namespace Glance.ThemeSwitcher;

public static class SolarCalculator
{
    private const double Zenith = 90.833;

    public static SolarSchedule? Calculate(DateOnly date,
        double latitude,
        double longitude,
        TimeZoneInfo timeZone)
    {
        DateTimeOffset? sunriseUtc = CalculateUtc(date, latitude, longitude, true);
        DateTimeOffset? sunsetUtc = CalculateUtc(date, latitude, longitude, false);

        return sunriseUtc is null || sunsetUtc is null
            ? null
            : new SolarSchedule(TimeZoneInfo.ConvertTime(sunriseUtc.Value, timeZone), TimeZoneInfo.ConvertTime(sunsetUtc.Value, timeZone));
    }

    private static DateTimeOffset? CalculateUtc(DateOnly date,
        double latitude,
        double longitude,
        bool sunrise)
    {
        int day = date.DayOfYear;
        double longitudeHour = longitude / 15;
        double approximateTime = day + (((sunrise ? 6 : 18) - longitudeHour) / 24);
        double meanAnomaly = (0.9856 * approximateTime) - 3.289;
        double trueLongitude = NormalizeDegrees(meanAnomaly + (1.916 * Sin(meanAnomaly)) + (0.020 * Sin(2 * meanAnomaly)) + 282.634);
        double rightAscension = NormalizeDegrees(RadiansToDegrees(Math.Atan(0.91764 * Math.Tan(DegreesToRadians(trueLongitude)))));
        rightAscension += (Math.Floor(trueLongitude / 90) * 90) - (Math.Floor(rightAscension / 90) * 90);
        rightAscension /= 15;

        double sinDeclination = 0.39782 * Sin(trueLongitude);
        double cosDeclination = Math.Cos(Math.Asin(sinDeclination));
        double cosHourAngle = (Math.Cos(DegreesToRadians(Zenith)) - (sinDeclination * Sin(latitude))) / (cosDeclination * Math.Cos(DegreesToRadians(latitude)));

        if (cosHourAngle is > 1 or < -1)
        {
            return null;
        }

        double hourAngle = sunrise
            ? 360 - RadiansToDegrees(Math.Acos(cosHourAngle))
            : RadiansToDegrees(Math.Acos(cosHourAngle));
        hourAngle /= 15;

        double localMeanTime = hourAngle + rightAscension - (0.06571 * approximateTime) - 6.622;
        double utcHours = NormalizeHours(localMeanTime - longitudeHour);
        DateTime utcDate = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return new DateTimeOffset(utcDate).AddHours(utcHours);
    }

    private static double DegreesToRadians(double value) => value * Math.PI / 180;

    private static double NormalizeDegrees(double value) => ((value % 360) + 360) % 360;

    private static double NormalizeHours(double value) => ((value % 24) + 24) % 24;

    private static double RadiansToDegrees(double value) => value * 180 / Math.PI;

    private static double Sin(double value) => Math.Sin(DegreesToRadians(value));
}
