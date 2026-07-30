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
        WeatherSnapshot snapshot = new("London", 18.4, 17.2, 70, 4.8, "light rain", WeatherSceneKind.Rain, true, DateTimeOffset.UtcNow);

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
    }

    private sealed class TestTextLocalizer :
        ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) =>
            arguments.Length == 0 ? key : $"{key}({string.Join(',', arguments)})";
    }
}
