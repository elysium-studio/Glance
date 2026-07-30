namespace Glance.Weather;

public sealed record WeatherSnapshot(string Location,
    double Temperature,
    double FeelsLike,
    int Humidity,
    double WindSpeed,
    string Condition,
    WeatherSceneKind Scene,
    bool IsDay,
    WeatherTimeOfDay TimeOfDay,
    WeatherSky Sky,
    WeatherEffect Effect,
    WeatherTemperature TemperatureState,
    DateTimeOffset UpdatedAt);
