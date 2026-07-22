namespace Glance.AudioSwitcher;

public interface IAudioDeviceService
{
    event EventHandler? DevicesChanged;

    IReadOnlyList<AudioOutputDevice> GetOutputDevices();

    bool TrySetDefaultOutput(string deviceId);

    bool TrySetOutputMuted(string deviceId, bool isMuted);
}
