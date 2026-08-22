namespace Glance.Fasting;

public static class FastingPlanCatalog
{
    public static TimeSpan GetFastingDuration(FastingPlan plan, double customHours) => plan switch
    {
        FastingPlan.TwelveTwelve => TimeSpan.FromHours(12),
        FastingPlan.FourteenTen => TimeSpan.FromHours(14),
        FastingPlan.SixteenEight => TimeSpan.FromHours(16),
        FastingPlan.EighteenSix => TimeSpan.FromHours(18),
        FastingPlan.TwentyFour => TimeSpan.FromHours(20),
        FastingPlan.Custom => TimeSpan.FromHours(NormalizeCustomHours(customHours)),
        _ => TimeSpan.FromHours(16)
    };

    public static double NormalizeCustomHours(double value) => Math.Clamp(value, 1, 48);
}
