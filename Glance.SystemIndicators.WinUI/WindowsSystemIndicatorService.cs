using Glance.SystemIndicators;
using Microsoft.UI.Dispatching;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System.Management;
using System.Runtime.InteropServices;

namespace Glance.SystemIndicators.WinUI;

public sealed partial class WindowsSystemIndicatorService :
    ISystemIndicatorService,
    IMMNotificationClient
{
    private const int HookKeyboardLowLevel = 13;
    private const int VirtualKeyCapsLock = 0x14;
    private const int VirtualKeyNumLock = 0x90;
    private const int VirtualKeyScrollLock = 0x91;
    private const int VirtualKeyVolumeMute = 0xAD;
    private const int VirtualKeyVolumeDown = 0xAE;
    private const int VirtualKeyVolumeUp = 0xAF;
    private const int WindowMessageKeyDown = 0x0100;
    private const int WindowMessageSystemKeyDown = 0x0104;
    private const int WindowMessageKeyUp = 0x0101;
    private const int WindowMessageSystemKeyUp = 0x0105;
    private readonly CancellationTokenSource cancellation = new();
    private readonly DispatcherQueue dispatcherQueue;
    private readonly object volumeGate = new();
    private readonly MMDeviceEnumerator deviceEnumerator = new();
    private readonly KeyboardHookProcedure keyboardHookProcedure;
    private AudioEndpointVolume? endpointVolume;
    private AudioEndpointVolumeNotificationDelegate? endpointVolumeChanged;
    private MMDevice? outputDevice;
    private ManagementEventWatcher? brightnessWatcher;
    private nint keyboardHook;
    private volatile bool isDisposed;
    private volatile bool isEnabled = true;

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public WindowsSystemIndicatorService()
    {
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        keyboardHookProcedure = HandleKeyboardHook;
        _ = deviceEnumerator.RegisterEndpointNotificationCallback(this);
        RebindOutputDevice();
        StartBrightnessWatcher();
        keyboardHook = SetWindowsHookEx(HookKeyboardLowLevel,
            keyboardHookProcedure,
            GetModuleHandle(null),
            0);
        _ = WatchAirplaneModeAsync(cancellation.Token);
    }

    public event EventHandler<SystemIndicatorState>? StateChanged;

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        cancellation.Cancel();
        try
        {
            brightnessWatcher?.Stop();
        }
        catch
        {
        }

        if (brightnessWatcher is not null)
        {
            brightnessWatcher.EventArrived -= HandleBrightnessChanged;
            brightnessWatcher.Dispose();
            brightnessWatcher = null;
        }

        if (keyboardHook != nint.Zero)
        {
            _ = UnhookWindowsHookEx(keyboardHook);
            keyboardHook = nint.Zero;
        }

        _ = deviceEnumerator.UnregisterEndpointNotificationCallback(this);
        ReleaseOutputDevice();
        deviceEnumerator.Dispose();
        cancellation.Dispose();
    }

    public void OnDeviceAdded(string deviceId)
    {
    }

    public void OnDeviceRemoved(string deviceId) => RebindOutputDevice();

    public void OnDeviceStateChanged(string deviceId,
        DeviceState newState) => RebindOutputDevice();

    public void OnDefaultDeviceChanged(DataFlow flow,
        Role role,
        string defaultDeviceId)
    {
        if ((flow is DataFlow.Render or DataFlow.All) &&
            (role is Role.Multimedia or Role.Console))
        {
            RebindOutputDevice();
        }
    }

    public void OnPropertyValueChanged(string deviceId,
        PropertyKey key)
    {
    }

    private void RebindOutputDevice()
    {
        lock (volumeGate)
        {
            ReleaseOutputDeviceCore();

            if (isDisposed)
            {
                return;
            }

            try
            {
                outputDevice = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                endpointVolume = outputDevice.AudioEndpointVolume;
                endpointVolumeChanged = _ => PublishVolume();
                endpointVolume.OnVolumeNotification += endpointVolumeChanged;
            }
            catch
            {
                ReleaseOutputDeviceCore();
            }
        }
    }

    private void ReleaseOutputDevice()
    {
        lock (volumeGate)
        {
            ReleaseOutputDeviceCore();
        }
    }

    private void ReleaseOutputDeviceCore()
    {
        if (endpointVolume is not null && endpointVolumeChanged is not null)
        {
            endpointVolume.OnVolumeNotification -= endpointVolumeChanged;
        }

        endpointVolumeChanged = null;
        endpointVolume?.Dispose();
        endpointVolume = null;
        outputDevice?.Dispose();
        outputDevice = null;
    }

    private void PublishVolume()
    {
        int level;
        bool isMuted;

        lock (volumeGate)
        {
            if (isDisposed || endpointVolume is null)
            {
                return;
            }

            try
            {
                level = Math.Clamp((int)Math.Round(endpointVolume.MasterVolumeLevelScalar * 100,
                    MidpointRounding.AwayFromZero),
                    0,
                    100);
                isMuted = endpointVolume.Mute;
            }
            catch
            {
                return;
            }
        }

        Publish(new SystemIndicatorState(SystemIndicatorKind.Volume,
            level,
            !isMuted));
    }

    private void StartBrightnessWatcher()
    {
        try
        {
            ManagementScope scope = new("\\\\.\\root\\WMI");
            WqlEventQuery query = new("SELECT * FROM WmiMonitorBrightnessEvent");
            brightnessWatcher = new ManagementEventWatcher(scope, query);
            brightnessWatcher.EventArrived += HandleBrightnessChanged;
            brightnessWatcher.Start();
        }
        catch
        {
            brightnessWatcher?.Dispose();
            brightnessWatcher = null;
        }
    }

    private void HandleBrightnessChanged(object sender,
        EventArrivedEventArgs args)
    {
        try
        {
            int level = Convert.ToInt32(args.NewEvent.Properties["Brightness"].Value,
                System.Globalization.CultureInfo.InvariantCulture);
            Publish(new SystemIndicatorState(SystemIndicatorKind.Brightness,
                Math.Clamp(level, 0, 100)));
        }
        catch
        {
        }
    }

    private nint HandleKeyboardHook(int code,
        nuint message,
        nint data)
    {
        if (code < 0)
        {
            return CallNextHookEx(keyboardHook, code, message, data);
        }

        uint virtualKey = unchecked((uint)Marshal.ReadInt32(data));

        if (IsEnabled &&
            (message is WindowMessageKeyDown or WindowMessageSystemKeyDown) &&
            (virtualKey is VirtualKeyVolumeMute or VirtualKeyVolumeDown or VirtualKeyVolumeUp) &&
            TryHandleVolumeKey(virtualKey))
        {
            return 1;
        }

        if (message is WindowMessageKeyUp or WindowMessageSystemKeyUp)
        {
            if (IsEnabled &&
                (virtualKey is VirtualKeyVolumeMute or VirtualKeyVolumeDown or VirtualKeyVolumeUp))
            {
                return 1;
            }

            SystemIndicatorKind? kind = virtualKey switch
            {
                VirtualKeyCapsLock => SystemIndicatorKind.CapsLock,
                VirtualKeyNumLock => SystemIndicatorKind.NumLock,
                VirtualKeyScrollLock => SystemIndicatorKind.ScrollLock,
                _ => null
            };

            if (kind is not null)
            {
                _ = dispatcherQueue.TryEnqueue(() =>
                {
                    bool isEnabled = (GetKeyState((int)virtualKey) & 1) != 0;
                    Publish(new SystemIndicatorState(kind.Value,
                        IsEnabled: isEnabled));
                });
            }
        }

        return CallNextHookEx(keyboardHook, code, message, data);
    }

    private bool TryHandleVolumeKey(uint virtualKey)
    {
        bool handled;

        lock (volumeGate)
        {
            if (isDisposed || endpointVolume is null)
            {
                return false;
            }

            try
            {
                if (virtualKey == VirtualKeyVolumeMute)
                {
                    endpointVolume.Mute = !endpointVolume.Mute;
                }
                else
                {
                    float delta = virtualKey == VirtualKeyVolumeUp ? 0.02f : -0.02f;
                    endpointVolume.MasterVolumeLevelScalar = Math.Clamp(endpointVolume.MasterVolumeLevelScalar + delta,
                        0,
                        1);
                }

                handled = true;
            }
            catch
            {
                return false;
            }
        }

        if (handled)
        {
            PublishVolume();
        }

        return handled;
    }

    private async Task WatchAirplaneModeAsync(CancellationToken cancellationToken)
    {
        bool? previousState = ReadAirplaneModeState();

        try
        {
            using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(750));

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                bool? currentState = ReadAirplaneModeState();

                if (currentState is null || currentState == previousState)
                {
                    continue;
                }

                previousState = currentState;
                Publish(new SystemIndicatorState(SystemIndicatorKind.AirplaneMode,
                    IsEnabled: currentState));
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static bool? ReadAirplaneModeState()
    {
        try
        {
            using RegistryKey? management = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\RadioManagement");
            object? value = management?.GetValue("SystemRadioState");

            if (value is null)
            {
                using RegistryKey? state = management?.OpenSubKey("SystemRadioState");
                value = state?.GetValue(null);
            }

            return value is null ? null : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) != 0;
        }
        catch
        {
            return null;
        }
    }

    private void Publish(SystemIndicatorState state)
    {
        if (IsEnabled && !isDisposed)
        {
            StateChanged?.Invoke(this, state);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint KeyboardHookProcedure(int code,
        nuint message,
        nint data);

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int hook,
        KeyboardHookProcedure callback,
        nint module,
        uint threadId);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWindowsHookEx(nint hook);

    [LibraryImport("user32.dll")]
    private static partial nint CallNextHookEx(nint hook,
        int code,
        nuint message,
        nint data);

    [LibraryImport("user32.dll")]
    private static partial short GetKeyState(int virtualKey);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint GetModuleHandle(string? moduleName);
}
