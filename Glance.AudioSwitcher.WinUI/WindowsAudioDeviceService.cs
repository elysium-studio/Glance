using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Glance.AudioSwitcher.WinUI;

public sealed class WindowsAudioDeviceService :
    IAudioDeviceService,
    IMMNotificationClient,
    IDisposable
{
    private readonly MMDeviceEnumerator deviceEnumerator = new();
    private readonly Dictionary<string, TrackedOutputDevice> trackedOutputDevices = new(StringComparer.OrdinalIgnoreCase);
    private readonly object gate = new();
    private bool isDisposed;

    public WindowsAudioDeviceService() =>
        deviceEnumerator.RegisterEndpointNotificationCallback(this);

    public event EventHandler? DevicesChanged;

    public IReadOnlyList<AudioOutputDevice> GetOutputDevices()
    {
        List<AudioOutputDevice> devices = [];
        HashSet<string> activeDeviceIds = new(StringComparer.OrdinalIgnoreCase);
        string? defaultDeviceId = GetDefaultDeviceId();

        try
        {
            MMDeviceCollection endpoints = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            for (int index = 0; index < endpoints.Count; index++)
            {
                MMDevice endpoint = endpoints[index];
                activeDeviceIds.Add(endpoint.ID);

                try
                {
                    TrackedOutputDevice trackedDevice = GetOrTrack(endpoint);
                    devices.Add(new AudioOutputDevice(trackedDevice.Id, trackedDevice.Name, string.Equals(trackedDevice.Id, defaultDeviceId, StringComparison.OrdinalIgnoreCase), trackedDevice.VolumePercent, trackedDevice.IsMuted));
                }
                catch (Exception)
                {
                    endpoint.Dispose();
                }
            }

            RemoveInactiveDevices(activeDeviceIds);
        }
        catch (Exception)
        {
        }

        devices.Sort((left, right) =>
            StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name));

        return devices;
    }

    public bool TrySetDefaultOutput(string deviceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        object? policyClient = null;

        try
        {
            policyClient = new PolicyConfigClient();
            IPolicyConfig policyConfig = (IPolicyConfig)policyClient;

            SetDefaultEndpoint(policyConfig, deviceId, AudioDeviceRole.Console);
            SetDefaultEndpoint(policyConfig, deviceId, AudioDeviceRole.Multimedia);
            SetDefaultEndpoint(policyConfig, deviceId, AudioDeviceRole.Communications);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            if (policyClient is not null && Marshal.IsComObject(policyClient))
            {
                Marshal.FinalReleaseComObject(policyClient);
            }
        }
    }

    public bool TrySetOutputMuted(string deviceId, bool isMuted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        try
        {
            TrackedOutputDevice? trackedDevice;

            lock (gate)
            {
                trackedOutputDevices.TryGetValue(deviceId, out trackedDevice);
            }

            if (trackedDevice is not null)
            {
                return trackedDevice.TrySetMuted(isMuted);
            }

            using MMDevice endpoint = deviceEnumerator.GetDevice(deviceId);
            endpoint.AudioEndpointVolume.Mute = isMuted;
            return endpoint.AudioEndpointVolume.Mute == isMuted;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        deviceEnumerator.UnregisterEndpointNotificationCallback(this);

        lock (gate)
        {
            foreach (TrackedOutputDevice device in trackedOutputDevices.Values)
            {
                device.Dispose();
            }

            trackedOutputDevices.Clear();
        }

        deviceEnumerator.Dispose();
    }

    public void OnDeviceAdded(string deviceId) => RaiseDevicesChanged();

    public void OnDeviceRemoved(string deviceId) => RaiseDevicesChanged();

    public void OnDeviceStateChanged(string deviceId, DeviceState newState) => RaiseDevicesChanged();

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow is DataFlow.Render or DataFlow.All)
        {
            RaiseDevicesChanged();
        }
    }

    public void OnPropertyValueChanged(string deviceId, PropertyKey key) => RaiseDevicesChanged();

    private string? GetDefaultDeviceId()
    {
        try
        {
            using MMDevice endpoint = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return endpoint.ID;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void RaiseDevicesChanged() =>
        DevicesChanged?.Invoke(this, EventArgs.Empty);

    private TrackedOutputDevice GetOrTrack(MMDevice endpoint)
    {
        lock (gate)
        {
            if (trackedOutputDevices.TryGetValue(endpoint.ID, out TrackedOutputDevice? existing))
            {
                endpoint.Dispose();
                return existing;
            }

            TrackedOutputDevice trackedDevice = new(endpoint, RaiseDevicesChanged);
            trackedOutputDevices.Add(endpoint.ID, trackedDevice);
            return trackedDevice;
        }
    }

    private void RemoveInactiveDevices(IReadOnlySet<string> activeDeviceIds)
    {
        lock (gate)
        {
            string[] inactiveDeviceIds = trackedOutputDevices.Keys.Where(id => !activeDeviceIds.Contains(id)).ToArray();

            foreach (string deviceId in inactiveDeviceIds)
            {
                trackedOutputDevices.Remove(deviceId, out TrackedOutputDevice? device);
                device?.Dispose();
            }
        }
    }

    private static void SetDefaultEndpoint(
        IPolicyConfig policyConfig,
        string deviceId,
        AudioDeviceRole role) =>
        Marshal.ThrowExceptionForHR(policyConfig.SetDefaultEndpoint(deviceId, role));

    private sealed class TrackedOutputDevice :
        IDisposable
    {
        private readonly AudioEndpointVolume endpointVolume;
        private readonly AudioEndpointVolumeNotificationDelegate volumeChanged;
        private readonly MMDevice device;

        public TrackedOutputDevice(
            MMDevice device,
            Action changed)
        {
            this.device = device;
            endpointVolume = device.AudioEndpointVolume;
            volumeChanged = _ => changed();
            endpointVolume.OnVolumeNotification += volumeChanged;
        }

        public string Id => device.ID;

        public string Name => device.FriendlyName;

        public int VolumePercent => Math.Clamp((int)Math.Round(endpointVolume.MasterVolumeLevelScalar * 100, MidpointRounding.AwayFromZero), 0, 100);

        public bool IsMuted => endpointVolume.Mute;

        public bool TrySetMuted(bool isMuted)
        {
            endpointVolume.Mute = isMuted;
            return endpointVolume.Mute == isMuted;
        }

        public void Dispose()
        {
            endpointVolume.OnVolumeNotification -= volumeChanged;
            endpointVolume.Dispose();
            device.Dispose();
        }
    }
}

internal enum AudioDeviceRole
{
    Console,
    Multimedia,
    Communications
}

[ComImport]
[Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
internal sealed class PolicyConfigClient;

[ComImport]
[Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    [PreserveSig]
    int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr format);

    [PreserveSig]
    int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int defaultFormat, IntPtr format);

    [PreserveSig]
    int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

    [PreserveSig]
    int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr endpointFormat, IntPtr mixFormat);

    [PreserveSig]
    int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int defaultPeriod, IntPtr defaultValue, IntPtr minimumValue);

    [PreserveSig]
    int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr period);

    [PreserveSig]
    int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr mode);

    [PreserveSig]
    int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr mode);

    [PreserveSig]
    int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr key, IntPtr value);

    [PreserveSig]
    int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr key, IntPtr value);

    [PreserveSig]
    int SetDefaultEndpoint(
        [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
        AudioDeviceRole role);

    [PreserveSig]
    int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int visible);
}
