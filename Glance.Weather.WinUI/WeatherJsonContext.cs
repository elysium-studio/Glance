using System.Text.Json.Serialization;

namespace Glance.Weather.WinUI;

[JsonSerializable(typeof(WeatherSettings))]
[JsonSerializable(typeof(OpenWeatherResponse))]
internal sealed partial class WeatherJsonContext :
    JsonSerializerContext;
