namespace Glance.Weather;

public sealed class WeatherSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public bool UseFahrenheit { get; set; }

    public WeatherTimeOfDay PreviewTime { get; set; }

    public WeatherSky PreviewSky { get; set; }

    public WeatherEffect PreviewEffect { get; set; }

    public WeatherTemperature PreviewTemperature { get; set; }
}
