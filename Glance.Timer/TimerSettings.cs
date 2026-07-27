namespace Glance.Timer;

public sealed class TimerSettings
{
    public double AdjustmentMinutes { get; set; } = 1;

    public double DefaultDurationMinutes { get; set; } = 5;

    public bool ResumeAutomatically { get; set; }

    public long SessionDurationTicks { get; set; }

    public long SessionRemainingTicks { get; set; }

    public DateTimeOffset SessionUpdatedUtc { get; set; }

    public bool SessionWasRunning { get; set; }
}
