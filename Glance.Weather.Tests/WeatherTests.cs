using Glance.Application.Abstractions;
using Xunit;

namespace Glance.Weather.Tests;

public sealed class WeatherTests
{
    [Theory]
    [InlineData(201, 20, false, WeatherSceneKind.Thunderstorm)]
    [InlineData(500, 20, false, WeatherSceneKind.Rain)]
    [InlineData(601, 2, false, WeatherSceneKind.Snow)]
    [InlineData(741, 12, false, WeatherSceneKind.Fog)]
    [InlineData(800, 32, false, WeatherSceneKind.Hot)]
    [InlineData(800, 28, false, WeatherSceneKind.Clear)]
    [InlineData(801, 20, false, WeatherSceneKind.PartlyCloudy)]
    [InlineData(804, 20, false, WeatherSceneKind.Cloudy)]
    public void ConditionMapper_MapsOpenWeatherCodes(int code, double temperature, bool useFahrenheit, WeatherSceneKind expected) =>
        Assert.Equal(expected, WeatherConditionMapper.Map(code, temperature, useFahrenheit));

    [Fact]
    public void Update_FormatsCurrentWeather()
    {
        WeatherViewModel viewModel = new(new TestTextLocalizer());
        WeatherSnapshot snapshot = new("London",
            18.4,
            17.2,
            70,
            4.8,
            "light rain",
            WeatherSceneKind.Rain,
            true,
            WeatherTimeOfDay.Afternoon,
            WeatherSky.PartlyCloudy,
            WeatherEffect.Rain,
            WeatherTemperature.Normal,
            DateTimeOffset.UtcNow);

        viewModel.Update(snapshot, false);

        Assert.Equal("18°", viewModel.TemperatureText);
        Assert.Equal("18° · Light rain", viewModel.CompactStatusText);
        Assert.Equal("London", viewModel.LocationText);
        Assert.Equal(WeatherSceneKind.Rain, viewModel.Scene);
        Assert.True(viewModel.HasWeatherData);
    }

    [Fact]
    public void Settings_DefaultToMetricUnits()
    {
        WeatherSettings settings = new();

        Assert.False(settings.UseFahrenheit);
        Assert.Empty(settings.ApiKey);
        Assert.Empty(settings.Location);
        Assert.Equal(WeatherTimeOfDay.Live, settings.PreviewTime);
        Assert.Equal(WeatherSky.Live, settings.PreviewSky);
        Assert.Equal(WeatherCelestial.Live, settings.PreviewCelestial);
        Assert.Equal(WeatherEffect.Live, settings.PreviewEffect);
        Assert.Equal(WeatherTemperature.Live, settings.PreviewTemperature);
    }

    [Theory]
    [InlineData(0, WeatherSky.Clear)]
    [InlineData(45, WeatherSky.PartlyCloudy)]
    [InlineData(90, WeatherSky.Cloudy)]
    public void ConditionMapper_MapsCloudCoverIndependently(int cloudCover, WeatherSky expected) =>
        Assert.Equal(expected, WeatherConditionMapper.MapSky(cloudCover));

    [Fact]
    public void ConditionMapper_ComposesSnowWithClearSky()
    {
        Assert.Equal(WeatherSky.Clear, WeatherConditionMapper.MapSky(5));
        Assert.Equal(WeatherEffect.Snow, WeatherConditionMapper.MapEffect(601));
    }

    [Theory]
    [InlineData(20000, 10000, 50000, WeatherTimeOfDay.Morning)]
    [InlineData(32000, 10000, 50000, WeatherTimeOfDay.Afternoon)]
    [InlineData(47000, 10000, 50000, WeatherTimeOfDay.Evening)]
    [InlineData(10000, 10000, 50000, WeatherTimeOfDay.Dawn)]
    [InlineData(51000, 10000, 50000, WeatherTimeOfDay.Dusk)]
    [InlineData(60000, 10000, 50000, WeatherTimeOfDay.Night)]
    public void ConditionMapper_MapsTimeIndependently(long timestamp, long sunrise, long sunset, WeatherTimeOfDay expected) =>
        Assert.Equal(expected, WeatherConditionMapper.MapTime(timestamp, sunrise, sunset));

    [Theory]
    [InlineData(WeatherTimeOfDay.Afternoon, WeatherSky.PartlyCloudy, WeatherCelestial.Sun)]
    [InlineData(WeatherTimeOfDay.Dusk, WeatherSky.Clear, WeatherCelestial.None)]
    [InlineData(WeatherTimeOfDay.Night, WeatherSky.Clear, WeatherCelestial.Moon)]
    [InlineData(WeatherTimeOfDay.Night, WeatherSky.Cloudy, WeatherCelestial.None)]
    public void ConditionMapper_MapsLiveCelestialBody(WeatherTimeOfDay time, WeatherSky sky, WeatherCelestial expected) =>
        Assert.Equal(expected, WeatherConditionMapper.MapCelestial(time, sky));

    [Fact]
    public void Update_HidesCelestialBodyUnderCloudySky()
    {
        WeatherViewModel viewModel = new(new TestTextLocalizer());
        WeatherSnapshot snapshot = new("London",
            10,
            9,
            80,
            3,
            "overcast clouds",
            WeatherSceneKind.Cloudy,
            false,
            WeatherTimeOfDay.Night,
            WeatherSky.Cloudy,
            WeatherEffect.None,
            WeatherTemperature.Normal,
            DateTimeOffset.UtcNow);

        viewModel.Update(snapshot, false);

        Assert.Equal(WeatherCelestial.None, viewModel.WeatherCelestial);
    }

    private sealed class TestTextLocalizer :
        ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) =>
            arguments.Length == 0 ? key : $"{key}({string.Join(',', arguments)})";
    }
}
