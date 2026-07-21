namespace Glance.AudioSwitcher;

public sealed record AudioOutputDevice(
    string Id,
    string Name,
    bool IsDefault);
