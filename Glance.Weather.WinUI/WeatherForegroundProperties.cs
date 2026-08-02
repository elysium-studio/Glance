namespace Glance.Weather.WinUI;

internal static class WeatherForegroundProperties
{
    public static bool Contains(string? propertyName) => propertyName is nameof(WeatherViewModel.WeatherHour) or
        nameof(WeatherViewModel.SunriseHour) or
        nameof(WeatherViewModel.SunsetHour) or
        nameof(WeatherViewModel.WeatherTime) or
        nameof(WeatherViewModel.WeatherSky) or
        nameof(WeatherViewModel.WeatherEffect) or
        nameof(WeatherViewModel.WeatherTemperature);
}
