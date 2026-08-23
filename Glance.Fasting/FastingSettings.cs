namespace Glance.Fasting;

public sealed class FastingSettings
{
    public FastingPlan Plan { get; set; } = FastingPlan.SixteenEight;

    public double CustomFastingHours { get; set; } = 16;

    public double CustomEatingHours { get; set; } = 8;

    public FastingSessionStatus SessionStatus { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public bool CompletionAttentionSent { get; set; }
}
