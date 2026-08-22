namespace Glance.Hydration.Tests;

public sealed class HydrationReminderPolicyTests
{
    private readonly HydrationReminderPolicy policy = new();
    private readonly HydrationSettings settings = new();

    [Fact]
    public void ExpectedProgressFollowsConfiguredDay()
    {
        DateTimeOffset midday = new(2026, 8, 22, 15, 0, 0, TimeSpan.Zero);

        Assert.Equal(0.5, policy.GetExpectedProgress(settings, midday), 3);
    }

    [Theory]
    [InlineData(1600, HydrationLevel.OnTrack)]
    [InlineData(1200, HydrationLevel.Behind)]
    [InlineData(600, HydrationLevel.Critical)]
    [InlineData(2000, HydrationLevel.GoalReached)]
    public void LevelReflectsProgressAgainstTime(double consumed, HydrationLevel expected)
    {
        DateTimeOffset now = new(2026, 8, 22, 19, 0, 0, TimeSpan.Zero);
        HydrationSnapshot snapshot = new(consumed, 2000, default);

        Assert.Equal(expected, policy.GetLevel(settings, snapshot, now));
    }

    [Fact]
    public void BehindOnlyReminderStaysQuietWhenOnTrack()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        HydrationSnapshot snapshot = new(1000, 2000, default);

        Assert.False(policy.ShouldRemind(settings, snapshot, now));
    }

    [Fact]
    public void ReminderIsDeduplicatedWithinInterval()
    {
        DateTimeOffset now = new(2026, 8, 22, 14, 0, 0, TimeSpan.Zero);
        HydrationSnapshot snapshot = new(250, 2000, now.AddMinutes(-30));

        Assert.False(policy.ShouldRemind(settings, snapshot, now));
    }

    [Fact]
    public void ReminderIsDueWhenBehindAfterInterval()
    {
        DateTimeOffset now = new(2026, 8, 22, 14, 0, 0, TimeSpan.Zero);
        HydrationSnapshot snapshot = new(250, 2000, now.AddMinutes(-61));

        Assert.True(policy.ShouldRemind(settings, snapshot, now));
    }
}
