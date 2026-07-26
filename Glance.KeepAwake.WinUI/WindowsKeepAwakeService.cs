using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.System.Power;

namespace Glance.KeepAwake.WinUI;

public sealed class WindowsKeepAwakeService :
    IKeepAwakeService,
    IDisposable
{
    private readonly BlockingCollection<StateRequest> requests = [];
    private readonly Thread worker;
    private bool isDisposed;

    public WindowsKeepAwakeService()
    {
        worker = new(ProcessRequests)
        {
            IsBackground = true,
            Name = "Glance Keep Awake"
        };
        worker.Start();
    }

    public bool IsActive { get; private set; }

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
        foreach (StateRequest request in requests.GetConsumingEnumerable())
        {
            bool succeeded = SetExecutionState(request.IsActive);

            if (succeeded)
            {
                IsActive = request.IsActive;
            }

            request.Completion.SetResult(succeeded);

            if (request.IsShutdown)
            {
                return;
            }
        }
    }

    private static bool SetExecutionState(bool isActive)
    {
        EXECUTION_STATE state = isActive
            ? EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED
            : EXECUTION_STATE.ES_CONTINUOUS;

        return PInvoke.SetThreadExecutionState(state) != 0;
    }

    private sealed record StateRequest(bool IsActive,
        bool IsShutdown,
        TaskCompletionSource<bool> Completion);
}
