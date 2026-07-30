namespace Glance.Weather;

public sealed class WeatherSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public bool UseFahrenheit { get; set; }

    public bool DebugPreviewEnabled { get; set; }

    public double PreviewHour { get; set; } = 14;

    public WeatherSky PreviewSky { get; set; } = WeatherSky.PartlyCloudy;

    public WeatherCelestial PreviewCelestial { get; set; } = WeatherCelestial.Live;

    public WeatherEffect PreviewEffect { get; set; } = WeatherEffect.None;

    public WeatherTemperature PreviewTemperature { get; set; } = WeatherTemperature.Normal;
}
