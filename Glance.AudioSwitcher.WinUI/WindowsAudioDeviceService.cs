using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Glance.AudioSwitcher.WinUI;

public sealed class WindowsAudioDeviceService :
    IAudioDeviceService,
    IMMNotificationClient,
    IDisposable
{
    private readonly MMDeviceEnumerator deviceEnumerator = new();
    private bool isDisposed;

    public WindowsAudioDeviceService() =>
        deviceEnumerator.RegisterEndpointNotificationCallback(this);

    public event EventHandler? DevicesChanged;

    public IReadOnlyList<AudioOutputDevice> GetOutputDevices()
    {
        List<AudioOutputDevice> devices = [];
        string? defaultDeviceId = GetDefaultDeviceId();

        try
        {
            MMDeviceCollection endpoints = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            for (int index = 0; index < endpoints.Count; index++)
            {
                using MMDevice endpoint = endpoints[index];
                devices.Add(new AudioOutputDevice(endpoint.ID, endpoint.FriendlyName, string.Equals(endpoint.ID, defaultDeviceId, StringComparison.OrdinalIgnoreCase)));
            }
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

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        deviceEnumerator.UnregisterEndpointNotificationCallback(this);
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

    private static void SetDefaultEndpoint(
        IPolicyConfig policyConfig,
        string deviceId,
        AudioDeviceRole role) =>
        Marshal.ThrowExceptionForHR(policyConfig.SetDefaultEndpoint(deviceId, role));
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
