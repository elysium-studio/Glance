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
    private string glyph = "\uE706";

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
    private string statisticsText = localizer.GetText("WeatherDetailsUnavailable");

    [ObservableProperty]
    private string temperatureText = "--\u00B0";

    public string Title => localizer.GetText("ModuleTitle");

    public string RefreshLabel => localizer.GetText("RefreshWeather");

    public void SetNeedsConfiguration()
    {
        IsLoading = false;
        HasWeatherData = false;
        Scene = WeatherSceneKind.Unknown;
        CompactStatusText = localizer.GetText("ConfigureWeather");
        ConditionText = localizer.GetText("ConfigureWeather");
        DetailText = localizer.GetText("AddLocationAndApiKey");
        LocationText = localizer.GetText("LocationNotSet");
        StatisticsText = localizer.GetText("WeatherDetailsUnavailable");
        TemperatureText = "--\u00B0";
        Glyph = GetGlyph(Scene, true);
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
        TemperatureText = $"{Math.Round(snapshot.Temperature):0}\u00B0";
        ConditionText = Capitalize(snapshot.Condition);
        LocationText = snapshot.Location;
        CompactStatusText = $"{TemperatureText} \u00B7 {ConditionText}";
        DetailText = localizer.GetText("WeatherSourceDetail", snapshot.Location);
        StatisticsText = localizer.GetText("WeatherStatistics",
            Math.Round(snapshot.FeelsLike),
            snapshot.Humidity,
            FormatWind(snapshot.WindSpeed, useFahrenheit));
        Glyph = GetGlyph(Scene, snapshot.IsDay);
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

    private static string GetGlyph(WeatherSceneKind scene, bool isDay) =>
        scene switch
        {
            WeatherSceneKind.Clear => isDay ? "\uE706" : "\uE708",
            WeatherSceneKind.Hot => "\uE706",
            WeatherSceneKind.PartlyCloudy => "\uE9D7",
            WeatherSceneKind.Cloudy => "\uE753",
            WeatherSceneKind.Rain => "\uE9C4",
            WeatherSceneKind.Snow => "\uE9C8",
            WeatherSceneKind.Thunderstorm => "\uE9D2",
            WeatherSceneKind.Fog => "\uE9CB",
            _ => "\uE9CA"
        };
}
