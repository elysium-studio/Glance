using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Weather.WinUI;

internal sealed class OpenWeatherService(HttpClient httpClient) :
    IWeatherService
{
    private readonly HttpClient httpClient = httpClient;

    public async Task<WeatherSnapshot> GetCurrentAsync(WeatherSettings settings, CancellationToken cancellationToken)
    {
        string units = settings.UseFahrenheit ? "imperial" : "metric";
        string language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        string uri = $"https://api.openweathermap.org/data/2.5/weather?q={Uri.EscapeDataString(settings.Location)}&appid={Uri.EscapeDataString(settings.ApiKey)}&units={units}&lang={language}";
        using HttpResponseMessage response = await httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        OpenWeatherResponse weather = await JsonSerializer.DeserializeAsync(stream, WeatherJsonContext.Default.OpenWeatherResponse, cancellationToken) ??
            throw new InvalidOperationException("OpenWeather returned an empty response.");
        OpenWeatherCondition condition = weather.Weather.FirstOrDefault() ??
            throw new InvalidOperationException("OpenWeather returned no weather condition.");
        bool isDay = weather.Timestamp >= weather.System.Sunrise && weather.Timestamp < weather.System.Sunset;
        WeatherSceneKind scene = WeatherConditionMapper.Map(condition.Id, weather.Main.Temperature, settings.UseFahrenheit);

        return new WeatherSnapshot(weather.Name,
            weather.Main.Temperature,
            weather.Main.FeelsLike,
            weather.Main.Humidity,
            weather.Wind.Speed,
            condition.Description,
            scene,
            isDay,
            DateTimeOffset.FromUnixTimeSeconds(weather.Timestamp));
    }
}
