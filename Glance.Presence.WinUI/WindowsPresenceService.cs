using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Glance.Presence.WinUI;

public sealed class WindowsPresenceService :
    IPresenceService,
    IDisposable
{
    private const int PollIntervalMilliseconds = 15_000;
    private readonly PresenceActivityPolicy activityPolicy;
    private readonly BlockingCollection<StateRequest> requests = [];
    private readonly Thread worker;
    private volatile bool isActive;
    private bool isDisposed;
    private long lastPulseTimestamp;

    public WindowsPresenceService(PresenceActivityPolicy activityPolicy)
    {
        this.activityPolicy = activityPolicy;
        worker = new(ProcessRequests)
        {
            IsBackground = true,
            Name = "Glance Presence"
        };
        worker.Start();
    }

    public bool IsActive => isActive;

    public async Task<bool> SetActiveAsync(bool isActive,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        requests.Add(new StateRequest(isActive, false, completion), cancellationToken);
        return await completion.Task.WaitAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        requests.Add(new StateRequest(false, true, completion));
        completion.Task.GetAwaiter().GetResult();
        requests.CompleteAdding();
        worker.Join();
        requests.Dispose();
    }

    private void ProcessRequests()
    {
        while (true)
        {
            int timeout = isActive ? PollIntervalMilliseconds : Timeout.Infinite;

            if (requests.TryTake(out StateRequest? request, timeout))
            {
                bool succeeded = !request.IsActive || TryGetIdleDuration(out _);

                if (succeeded)
                {
                    isActive = request.IsActive;
                }

                request.Completion.TrySetResult(succeeded);

                if (request.IsShutdown)
                {
                    return;
                }

                continue;
            }

            MaintainPresence();
        }
    }

    private void MaintainPresence()
    {
        if (!TryGetIdleDuration(out TimeSpan idleDuration) ||
            !activityPolicy.ShouldSendInput(idleDuration) ||
            lastPulseTimestamp != 0 && Stopwatch.GetElapsedTime(lastPulseTimestamp) < activityPolicy.IdleThreshold)
        {
            return;
        }

        if (SendPresencePulse())
        {
            lastPulseTimestamp = Stopwatch.GetTimestamp();
        }
    }

    private static bool TryGetIdleDuration(out TimeSpan idleDuration)
    {
        LASTINPUTINFO information = new() { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };

        if (!PInvoke.GetLastInputInfo(ref information))
        {
            idleDuration = default;
            return false;
        }

        uint elapsedMilliseconds = unchecked((uint)Environment.TickCount - information.dwTime);
        idleDuration = TimeSpan.FromMilliseconds(elapsedMilliseconds);
        return true;
    }

    private static bool SendPresencePulse()
    {
        INPUT[] inputs =
        [
            CreateKey(VIRTUAL_KEY.VK_F15),
            CreateKey(VIRTUAL_KEY.VK_F15, KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP)
        ];

        return PInvoke.SendInput(inputs, Marshal.SizeOf<INPUT>()) == inputs.Length;
    }

    private static INPUT CreateKey(VIRTUAL_KEY key,
        KEYBD_EVENT_FLAGS flags = 0)
    {
        INPUT input = new() { type = INPUT_TYPE.INPUT_KEYBOARD };
        input.Anonymous.ki.wVk = key;
        input.Anonymous.ki.dwFlags = flags;
        return input;
    }

    private sealed record StateRequest(bool IsActive,
        bool IsShutdown,
        TaskCompletionSource<bool> Completion);
}
