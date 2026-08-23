namespace Glance.Fasting.Tests;

public sealed class FastingPlanCatalogTests
{
    [Theory]
    [InlineData(FastingPlan.TwelveTwelve, 12)]
    [InlineData(FastingPlan.FourteenTen, 14)]
    [InlineData(FastingPlan.SixteenEight, 16)]
    [InlineData(FastingPlan.EighteenSix, 18)]
    [InlineData(FastingPlan.TwentyFour, 20)]
    public void PredefinedPlansReturnExpectedDuration(FastingPlan plan, double hours) => Assert.Equal(hours, FastingPlanCatalog.GetFastingDuration(plan, 3).TotalHours);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(24, 24)]
    [InlineData(72, 48)]
    public void CustomDurationIsNormalized(double value, double expected) => Assert.Equal(expected, FastingPlanCatalog.GetFastingDuration(FastingPlan.Custom, value).TotalHours);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(24, 24)]
    [InlineData(48, 24)]
    public void CustomEatingWindowIsNormalized(double value, double expected) => Assert.Equal(expected, FastingPlanCatalog.NormalizeCustomEatingHours(value));
}
