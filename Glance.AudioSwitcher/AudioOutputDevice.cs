namespace Glance.AudioSwitcher;

public sealed record AudioOutputDevice(
    string Id,
    string Name,
    bool IsDefault,
    int VolumePercent = 0,
    bool IsMuted = false);
