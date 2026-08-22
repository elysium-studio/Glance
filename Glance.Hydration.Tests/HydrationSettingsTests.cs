namespace Glance.Hydration.Tests;

public sealed class HydrationSettingsTests
{
    [Theory]
    [InlineData(100, 500)]
    [InlineData(2000, 2000)]
    [InlineData(9000, 6000)]
    public void DailyGoalIsNormalized(double value, double expected) => Assert.Equal(expected, HydrationSettings.NormalizeDailyGoal(value));

    [Theory]
    [InlineData(10, 50)]
    [InlineData(250, 250)]
    [InlineData(1500, 1000)]
    public void ServingSizeIsNormalized(double value, double expected) => Assert.Equal(expected, HydrationSettings.NormalizeServingSize(value));

    [Fact]
    public void ReminderWindowAlwaysKeepsStartBeforeEnd()
    {
        TimeSpan start = HydrationSettings.NormalizeReminderStart(TimeSpan.FromHours(23), TimeSpan.FromHours(8));
        TimeSpan end = HydrationSettings.NormalizeReminderEnd(TimeSpan.FromHours(7), start);

        Assert.True(start < end);
    }
}
