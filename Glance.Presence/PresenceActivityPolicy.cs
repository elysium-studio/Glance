namespace Glance.Presence;

public sealed class PresenceActivityPolicy(TimeSpan idleThreshold)
{
    public TimeSpan IdleThreshold { get; } = idleThreshold > TimeSpan.Zero
        ? idleThreshold
        : throw new ArgumentOutOfRangeException(nameof(idleThreshold));

    public bool ShouldSendInput(TimeSpan idleDuration) =>
        idleDuration >= IdleThreshold;
}
