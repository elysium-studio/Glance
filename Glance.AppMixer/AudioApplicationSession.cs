namespace Glance.AppMixer;

public sealed record AudioApplicationSession(string Id,
    string DisplayName,
    int VolumePercent,
    bool IsMuted,
    double Peak,
    bool IsForeground,
    bool IsActive);
