namespace Glance.Weather;

public static class WeatherConditionMapper
{
    public static WeatherSceneKind Map(int conditionCode, double temperature, bool useFahrenheit) => conditionCode switch
    {
        >= 200 and <= 232 => WeatherSceneKind.Thunderstorm,
        >= 300 and <= 531 => WeatherSceneKind.Rain,
        >= 600 and <= 622 => WeatherSceneKind.Snow,
        >= 701 and <= 781 => WeatherSceneKind.Fog,
        800 when temperature >= (useFahrenheit ? 86 : 30) => WeatherSceneKind.Hot,
        800 => WeatherSceneKind.Clear,
        >= 801 and <= 802 => WeatherSceneKind.PartlyCloudy,
        >= 803 and <= 804 => WeatherSceneKind.Cloudy,
        _ => WeatherSceneKind.Unknown
    };

    public static WeatherSky MapSky(int cloudCover) => cloudCover switch
    {
        <= 10 => WeatherSky.Clear,
        <= 60 => WeatherSky.PartlyCloudy,
        _ => WeatherSky.Cloudy
    };

    public static WeatherEffect MapEffect(int conditionCode) => conditionCode switch
    {
        >= 200 and <= 232 => WeatherEffect.Thunderstorm,
        >= 300 and <= 531 => WeatherEffect.Rain,
        >= 600 and <= 622 => WeatherEffect.Snow,
        >= 701 and <= 781 => WeatherEffect.Fog,
        _ => WeatherEffect.None
    };

    public static WeatherTemperature MapTemperature(double temperature, bool useFahrenheit) => temperature >= (useFahrenheit ? 86 : 30) ? WeatherTemperature.Hot : WeatherTemperature.Normal;

    public static WeatherCelestial MapCelestial(WeatherTimeOfDay time, WeatherSky sky) => sky == WeatherSky.Cloudy ?
            WeatherCelestial.None :
            time switch
            {
                WeatherTimeOfDay.Night => WeatherCelestial.Moon,
                WeatherTimeOfDay.Dusk => WeatherCelestial.None,
                _ => WeatherCelestial.Sun
            };

    public static WeatherTimeOfDay MapTime(long timestamp, long sunrise, long sunset)
    {
        const long dawnBeforeSunriseSeconds = 45 * 60;
        const long dawnAfterSunriseSeconds = 30 * 60;
        const long eveningSeconds = 75 * 60;
        const long duskSeconds = 45 * 60;

        if (timestamp < sunrise - dawnBeforeSunriseSeconds)
        {
            return WeatherTimeOfDay.Night;
        }

        if (timestamp < sunrise + dawnAfterSunriseSeconds)
        {
            return WeatherTimeOfDay.Dawn;
        }

        if (timestamp >= sunset)
        {
            return timestamp < sunset + duskSeconds ?
                WeatherTimeOfDay.Dusk :
                WeatherTimeOfDay.Night;
        }

        long solarNoon = sunrise + ((sunset - sunrise) / 2);

        return timestamp < solarNoon
            ? WeatherTimeOfDay.Morning
            : timestamp < sunset - eveningSeconds ?
            WeatherTimeOfDay.Afternoon :
            WeatherTimeOfDay.Evening;
    }

    public static WeatherTimeOfDay MapTime(double hour, double sunrise, double sunset)
    {
        double dawnStart = NormalizeHour(sunrise - 0.75);
        double dawnEnd = NormalizeHour(sunrise + 0.5);
        double eveningStart = NormalizeHour(sunset - 1.25);
        double duskEnd = NormalizeHour(sunset + 0.75);
        double solarNoon = sunrise + ((sunset - sunrise) / 2);

        if (hour < dawnStart || hour >= duskEnd)
        {
            return WeatherTimeOfDay.Night;
        }

        if (hour < dawnEnd)
        {
            return WeatherTimeOfDay.Dawn;
        }

        if (hour < solarNoon)
        {
            return WeatherTimeOfDay.Morning;
        }

        return hour < eveningStart ? WeatherTimeOfDay.Afternoon : hour < sunset ? WeatherTimeOfDay.Evening : WeatherTimeOfDay.Dusk;
    }

    public static double GetLocalHour(long timestamp, int timeZoneOffset)
    {
        DateTimeOffset localTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).ToOffset(TimeSpan.FromSeconds(timeZoneOffset));
        return localTime.TimeOfDay.TotalHours;
    }

    private static double NormalizeHour(double hour) => ((hour % 24) + 24) % 24;
}
