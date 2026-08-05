using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using System.Globalization;

namespace Glance.Weather;

public sealed partial class WeatherViewModel(ITextLocalizer localizer) :
    ObservableObject
{
    private readonly ITextLocalizer localizer = localizer;

    [ObservableProperty]
    private string compactStatusText = localizer.GetText("ConfigureWeather");

    [ObservableProperty]
    private string accentColor = "#FF7DD3FC";

    [ObservableProperty]
    private string conditionText = localizer.GetText("WeatherUnavailable");

    [ObservableProperty]
    private string detailText = localizer.GetText("AddLocationAndApiKey");

    [ObservableProperty]
    private string iconPath = WeatherIconPaths.PartlyCloudyDay;

    [ObservableProperty]
    private bool hasWeatherData;

    [ObservableProperty]
    private bool isDay = true;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string locationText = localizer.GetText("LocationNotSet");

    [ObservableProperty]
    private WeatherSceneKind scene = WeatherSceneKind.Unknown;

    [ObservableProperty]
    private WeatherEffect weatherEffect = WeatherEffect.None;

    [ObservableProperty]
    private WeatherCelestial weatherCelestial = WeatherCelestial.Sun;

    [ObservableProperty]
    private WeatherSky weatherSky = WeatherSky.Clear;

    [ObservableProperty]
    private WeatherTemperature weatherTemperature = WeatherTemperature.Normal;

    [ObservableProperty]
    private WeatherTimeOfDay weatherTime = WeatherTimeOfDay.Afternoon;

    [ObservableProperty]
    private double weatherHour = 14;

    [ObservableProperty]
    private double sunriseHour = 6;

    [ObservableProperty]
    private double sunsetHour = 19;

    [ObservableProperty]
    private string statisticsText = localizer.GetText("WeatherDetailsUnavailable");

    [ObservableProperty]
    private string temperatureText = "--\u00B0";

    public string Title => localizer.GetText("ModuleTitle");

    public void SetNeedsConfiguration()
    {
        IsLoading = false;
        HasWeatherData = false;
        Scene = WeatherSceneKind.Unknown;
        WeatherEffect = WeatherEffect.None;
        WeatherCelestial = WeatherCelestial.Sun;
        WeatherSky = WeatherSky.Clear;
        WeatherTemperature = WeatherTemperature.Normal;
        WeatherTime = WeatherTimeOfDay.Afternoon;
        WeatherHour = 14;
        SunriseHour = 6;
        SunsetHour = 19;
        CompactStatusText = localizer.GetText("ConfigureWeather");
        ConditionText = localizer.GetText("ConfigureWeather");
        DetailText = localizer.GetText("AddLocationAndApiKey");
        LocationText = localizer.GetText("LocationNotSet");
        StatisticsText = localizer.GetText("WeatherDetailsUnavailable");
        TemperatureText = "--\u00B0";
        IconPath = GetIconPath(WeatherTime, WeatherSky, WeatherEffect);
        AccentColor = GetAccentColor(WeatherTime, WeatherSky, WeatherEffect, WeatherTemperature);
    }

    public void SetLoading()
    {
        IsLoading = true;

        if (!HasWeatherData)
        {
            CompactStatusText = localizer.GetText("UpdatingWeather");
            ConditionText = localizer.GetText("UpdatingWeather");
            DetailText = string.Empty;
        }
    }

    public void SetError()
    {
        IsLoading = false;

        if (!HasWeatherData)
        {
            CompactStatusText = localizer.GetText("WeatherUnavailable");
            ConditionText = localizer.GetText("WeatherUnavailable");
            DetailText = localizer.GetText("CheckWeatherSettings");
        }
    }

    public void Update(WeatherSnapshot snapshot, bool useFahrenheit)
    {
        IsLoading = false;
        HasWeatherData = true;
        Scene = snapshot.Scene;
        IsDay = snapshot.IsDay;
        WeatherEffect = snapshot.Effect;
        WeatherSky = snapshot.Sky;
        WeatherCelestial = WeatherConditionMapper.MapCelestial(snapshot.TimeOfDay, snapshot.Sky);
        WeatherTemperature = snapshot.TemperatureState;
        WeatherTime = snapshot.TimeOfDay;
        WeatherHour = WeatherConditionMapper.GetLocalHour(snapshot.UpdatedAt.ToUnixTimeSeconds(), snapshot.TimeZoneOffset);
        SunriseHour = WeatherConditionMapper.GetLocalHour(snapshot.Sunrise, snapshot.TimeZoneOffset);
        SunsetHour = WeatherConditionMapper.GetLocalHour(snapshot.Sunset, snapshot.TimeZoneOffset);
        TemperatureText = $"{Math.Round(snapshot.Temperature):0}\u00B0";
        ConditionText = Capitalize(snapshot.Condition);
        LocationText = snapshot.Location;
        CompactStatusText = $"{TemperatureText} \u00B7 {ConditionText}";
        DetailText = localizer.GetText("WeatherSourceDetail", snapshot.Location);
        StatisticsText = localizer.GetText("WeatherStatistics",
            Math.Round(snapshot.FeelsLike),
            snapshot.Humidity,
            FormatWind(snapshot.WindSpeed, useFahrenheit));
        IconPath = GetIconPath(snapshot.TimeOfDay, snapshot.Sky, snapshot.Effect);
        AccentColor = GetAccentColor(snapshot.TimeOfDay, snapshot.Sky, snapshot.Effect, snapshot.TemperatureState);
    }

    public void SetVisualState(WeatherTimeOfDay time,
        WeatherSky sky,
        WeatherCelestial celestial,
        WeatherEffect effect,
        WeatherTemperature temperature,
        double hour,
        double sunrise,
        double sunset)
    {
        IsDay = time is not WeatherTimeOfDay.Dusk and not WeatherTimeOfDay.Night;
        WeatherTime = time;
        WeatherHour = hour;
        SunriseHour = sunrise;
        SunsetHour = sunset;
        WeatherSky = sky;
        WeatherCelestial = celestial;
        WeatherEffect = effect;
        WeatherTemperature = temperature;
        IconPath = GetIconPath(time, sky, effect);
        AccentColor = GetAccentColor(time, sky, effect, temperature);
    }

    private string Capitalize(string value) => string.IsNullOrWhiteSpace(value)
            ? localizer.GetText("WeatherUnavailable")
            : value.Length == 1 ?
            char.ToUpper(value[0], CultureInfo.CurrentCulture).ToString() :
            $"{char.ToUpper(value[0], CultureInfo.CurrentCulture)}{value[1..]}";

    private static string FormatWind(double speed, bool useFahrenheit) => useFahrenheit ? $"{speed:0} mph" : $"{speed:0} m/s";

    private static string GetIconPath(WeatherTimeOfDay time, WeatherSky sky, WeatherEffect effect) => effect switch
    {
        WeatherEffect.Rain => WeatherIconPaths.Rain,
        WeatherEffect.Snow => WeatherIconPaths.Snow,
        WeatherEffect.Thunderstorm => WeatherIconPaths.Thunderstorm,
        WeatherEffect.Fog => WeatherIconPaths.Fog,
        _ => sky switch
        {
            WeatherSky.Cloudy => WeatherIconPaths.Cloudy,
            WeatherSky.PartlyCloudy => time is WeatherTimeOfDay.Dusk or WeatherTimeOfDay.Night ?
                WeatherIconPaths.PartlyCloudyNight :
                WeatherIconPaths.PartlyCloudyDay,
            _ => time is WeatherTimeOfDay.Dusk or WeatherTimeOfDay.Night ?
                WeatherIconPaths.Moon :
                WeatherIconPaths.Sunny
        }
    };

    private static string GetAccentColor(WeatherTimeOfDay time,
        WeatherSky sky,
        WeatherEffect effect,
        WeatherTemperature temperature) => effect switch
        {
            WeatherEffect.Rain => "#FF7DD3FC",
            WeatherEffect.Snow => "#FFF0F9FF",
            WeatherEffect.Thunderstorm => "#FFD8B4FE",
            WeatherEffect.Fog => "#FFD1D9E6",
            _ when temperature == WeatherTemperature.Hot => "#FFFFB45E",
            _ when sky == WeatherSky.Cloudy => "#FFD1D9E6",
            _ when time is WeatherTimeOfDay.Dusk or WeatherTimeOfDay.Night => "#FFB8C7FF",
            _ when sky == WeatherSky.PartlyCloudy => "#FF8ED8FF",
            _ => "#FFFFD166"
        };
}
