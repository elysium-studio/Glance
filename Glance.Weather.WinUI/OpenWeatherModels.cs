using System.Text.Json.Serialization;

namespace Glance.Weather.WinUI;

internal sealed class OpenWeatherResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("dt")]
    public long Timestamp { get; set; }

    [JsonPropertyName("timezone")]
    public int TimeZoneOffset { get; set; }

    [JsonPropertyName("main")]
    public OpenWeatherMeasurements Main { get; set; } = new();

    [JsonPropertyName("weather")]
    public OpenWeatherCondition[] Weather { get; set; } = [];

    [JsonPropertyName("wind")]
    public OpenWeatherWind Wind { get; set; } = new();

    [JsonPropertyName("clouds")]
    public OpenWeatherClouds Clouds { get; set; } = new();

    [JsonPropertyName("sys")]
    public OpenWeatherSunTimes System { get; set; } = new();
}

internal sealed class OpenWeatherClouds
{
    [JsonPropertyName("all")]
    public int Cover { get; set; }
}

internal sealed class OpenWeatherMeasurements
{
    [JsonPropertyName("temp")]
    public double Temperature { get; set; }

    [JsonPropertyName("feels_like")]
    public double FeelsLike { get; set; }

    [JsonPropertyName("humidity")]
    public int Humidity { get; set; }
}

internal sealed class OpenWeatherCondition
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

internal sealed class OpenWeatherWind
{
    [JsonPropertyName("speed")]
    public double Speed { get; set; }
}

internal sealed class OpenWeatherSunTimes
{
    [JsonPropertyName("sunrise")]
    public long Sunrise { get; set; }

    [JsonPropertyName("sunset")]
    public long Sunset { get; set; }
}
