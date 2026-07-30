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
}
