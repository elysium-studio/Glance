namespace Glance.Weather;

public interface IWeatherService
{
    Task<WeatherSnapshot> GetCurrentAsync(WeatherSettings settings, CancellationToken cancellationToken);
}
