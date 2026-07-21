using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;

namespace Glance.PrivacyControls.WinUI;

public sealed class WindowsMicrophoneService :
    IMicrophoneService,
    IMMNotificationClient,
    IDisposable
{
    private readonly MMDeviceEnumerator deviceEnumerator = new();
    private MMDevice? microphone;
    private volatile bool isDeviceInvalidated = true;
    private bool isDisposed;

    public WindowsMicrophoneService() =>
        deviceEnumerator.RegisterEndpointNotificationCallback(this);

    public MicrophoneState GetState()
    {
        try
        {
            MMDevice? device = GetMicrophone();

            if (device is null)
            {
                return MicrophoneState.Unavailable;
            }

            bool isMuted = device.AudioEndpointVolume.Mute;
            double peakLevel = isMuted
                ? 0
                : device.AudioMeterInformation.MasterPeakValue;
            return new MicrophoneState(device.FriendlyName, true, isMuted, peakLevel);
        }
        catch (Exception)
        {
            InvalidateDevice();
            return MicrophoneState.Unavailable;
        }
    }

    public bool TrySetMuted(bool isMuted)
    {
        try
        {
            MMDevice? device = GetMicrophone();

            if (device is null)
            {
                return false;
            }

            device.AudioEndpointVolume.Mute = isMuted;
            return device.AudioEndpointVolume.Mute == isMuted;
        }
        catch (Exception)
        {
            InvalidateDevice();
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
        microphone?.Dispose();
        deviceEnumerator.Dispose();
    }

    public void OnDeviceAdded(string deviceId) =>
        InvalidateDevice();

    public void OnDeviceRemoved(string deviceId) =>
        InvalidateDevice();

    public void OnDeviceStateChanged(string deviceId, DeviceState newState) =>
        InvalidateDevice();

    public void OnDefaultDeviceChanged(
        DataFlow flow,
        Role role,
        string defaultDeviceId)
    {
        if (flow is DataFlow.Capture or DataFlow.All)
        {
            InvalidateDevice();
        }
    }

    public void OnPropertyValueChanged(string deviceId, PropertyKey key) =>
        InvalidateDevice();

    private MMDevice? GetMicrophone()
    {
        if (!isDeviceInvalidated)
        {
            return microphone;
        }

        microphone?.Dispose();
        microphone = null;
        isDeviceInvalidated = false;

        try
        {
            microphone = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
        }
        catch (Exception)
        {
        }

        return microphone;
    }

    private void InvalidateDevice() =>
        isDeviceInvalidated = true;
}
