namespace Glance.Stopwatch;

public sealed class StopwatchSettings
{
    public long SessionElapsedTicks { get; set; }

    public DateTimeOffset SessionUpdatedUtc { get; set; }

    public bool SessionWasRunning { get; set; }

    public bool ResumeAutomatically { get; set; }
}
