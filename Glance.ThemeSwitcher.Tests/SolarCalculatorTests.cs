namespace Glance.ThemeSwitcher.Tests;

public sealed class SolarCalculatorTests
{
    [Fact]
    public void Calculate_ProducesExpectedEquatorialEquinoxTimes()
    {
        SolarSchedule? schedule = SolarCalculator.Calculate(new DateOnly(2026, 3, 20), 0, 0, TimeZoneInfo.Utc);

        Assert.NotNull(schedule);
        Assert.InRange(schedule.Sunrise.Hour, 5, 6);
        Assert.InRange(schedule.Sunset.Hour, 17, 18);
    }

    [Fact]
    public void Calculate_ProducesLongerLondonSummerDay()
    {
        TimeZoneInfo london = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        SolarSchedule? summer = SolarCalculator.Calculate(new DateOnly(2026, 6, 21), 51.5074, -0.1278, london);
        SolarSchedule? winter = SolarCalculator.Calculate(new DateOnly(2026, 12, 21), 51.5074, -0.1278, london);

        Assert.NotNull(summer);
        Assert.NotNull(winter);
        Assert.True(summer.Sunset - summer.Sunrise > winter.Sunset - winter.Sunrise);
    }

    [Fact]
    public void Calculate_ReturnsNullDuringPolarNight()
    {
        SolarSchedule? schedule = SolarCalculator.Calculate(new DateOnly(2026, 12, 21), 89, 0, TimeZoneInfo.Utc);

        Assert.Null(schedule);
    }
}
