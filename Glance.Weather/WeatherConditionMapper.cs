namespace Glance.Weather;

public static class WeatherConditionMapper
{
    public static WeatherSceneKind Map(int conditionCode, double temperature, bool useFahrenheit) =>
        conditionCode switch
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

    public static WeatherSky MapSky(int cloudCover) =>
        cloudCover switch
        {
            <= 10 => WeatherSky.Clear,
            <= 60 => WeatherSky.PartlyCloudy,
            _ => WeatherSky.Cloudy
        };

    public static WeatherEffect MapEffect(int conditionCode) =>
        conditionCode switch
        {
            >= 200 and <= 232 => WeatherEffect.Thunderstorm,
            >= 300 and <= 531 => WeatherEffect.Rain,
            >= 600 and <= 622 => WeatherEffect.Snow,
            >= 701 and <= 781 => WeatherEffect.Fog,
            _ => WeatherEffect.None
        };

    public static WeatherTemperature MapTemperature(double temperature, bool useFahrenheit) =>
        temperature >= (useFahrenheit ? 86 : 30) ? WeatherTemperature.Hot : WeatherTemperature.Normal;

    public static WeatherCelestial MapCelestial(WeatherTimeOfDay time, WeatherSky sky) =>
        sky == WeatherSky.Cloudy ?
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

        long solarNoon = sunrise + (sunset - sunrise) / 2;

        if (timestamp < solarNoon)
        {
            return WeatherTimeOfDay.Morning;
        }

        return timestamp < sunset - eveningSeconds ?
            WeatherTimeOfDay.Afternoon :
            WeatherTimeOfDay.Evening;
    }
}
