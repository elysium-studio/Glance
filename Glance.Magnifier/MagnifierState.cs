namespace Glance.Magnifier;

public sealed record MagnifierState(bool IsAvailable,
    bool IsRunning,
    double ZoomFactor);
