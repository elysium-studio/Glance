namespace Glance.FocusSession;

public sealed class FocusSessionSettings
{
    public double BreakDurationMinutes { get; set; } = 5;

    public double FocusDurationMinutes { get; set; } = 25;

    public bool ResumeAutomatically { get; set; }

    public int SessionCompletedFocusSessions { get; set; }

    public FocusSessionPhase SessionPhase { get; set; }

    public long SessionRemainingTicks { get; set; }

    public DateTimeOffset SessionUpdatedUtc { get; set; }

    public bool SessionWasRunning { get; set; }
}
