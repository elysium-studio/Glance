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
    private string conditionText = localizer.GetText("WeatherUnavailable");

    [ObservableProperty]
    private string detailText = localizer.GetText("AddLocationAndApiKey");

    [ObservableProperty]
    private string glyph = "\uE9C0";

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
        CompactStatusText = localizer.GetText("ConfigureWeather");
        ConditionText = localizer.GetText("ConfigureWeather");
        DetailText = localizer.GetText("AddLocationAndApiKey");
        LocationText = localizer.GetText("LocationNotSet");
        StatisticsText = localizer.GetText("WeatherDetailsUnavailable");
        TemperatureText = "--\u00B0";
        Glyph = GetGlyph(WeatherTime, WeatherSky, WeatherEffect);
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
        TemperatureText = $"{Math.Round(snapshot.Temperature):0}\u00B0";
        ConditionText = Capitalize(snapshot.Condition);
        LocationText = snapshot.Location;
        CompactStatusText = $"{TemperatureText} \u00B7 {ConditionText}";
        DetailText = localizer.GetText("WeatherSourceDetail", snapshot.Location);
        StatisticsText = localizer.GetText("WeatherStatistics",
            Math.Round(snapshot.FeelsLike),
            snapshot.Humidity,
            FormatWind(snapshot.WindSpeed, useFahrenheit));
        Glyph = GetGlyph(snapshot.TimeOfDay, snapshot.Sky, snapshot.Effect);
    }

    public void SetVisualState(WeatherTimeOfDay time, WeatherSky sky, WeatherCelestial celestial, WeatherEffect effect, WeatherTemperature temperature)
    {
        IsDay = time is not WeatherTimeOfDay.Dusk and not WeatherTimeOfDay.Night;
        WeatherTime = time;
        WeatherSky = sky;
        WeatherCelestial = celestial;
        WeatherEffect = effect;
        WeatherTemperature = temperature;
        Glyph = GetGlyph(time, sky, effect);
    }

    private string Capitalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return localizer.GetText("WeatherUnavailable");
        }

        return value.Length == 1 ?
            char.ToUpper(value[0], CultureInfo.CurrentCulture).ToString() :
            $"{char.ToUpper(value[0], CultureInfo.CurrentCulture)}{value[1..]}";
    }

    private static string FormatWind(double speed, bool useFahrenheit) =>
        useFahrenheit ? $"{speed:0} mph" : $"{speed:0} m/s";

    private static string GetGlyph(WeatherTimeOfDay time, WeatherSky sky, WeatherEffect effect) =>
        effect switch
        {
            WeatherEffect.Rain => "\uE9C4",
            WeatherEffect.Snow => "\uE9C8",
            WeatherEffect.Thunderstorm => "\uE9C6",
            WeatherEffect.Fog => "\uE9CB",
            _ => sky switch
            {
                WeatherSky.Cloudy => "\uE9BF",
                WeatherSky.PartlyCloudy => time is WeatherTimeOfDay.Dusk or WeatherTimeOfDay.Night ? "\uE9C1" : "\uE9C0",
                _ => time is WeatherTimeOfDay.Dusk or WeatherTimeOfDay.Night ? "\uE9C2" : "\uE706"
            }
        };
}
