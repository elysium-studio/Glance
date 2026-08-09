using Glance.Application.Abstractions;
using System;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Weather.WinUI;

public sealed class WeatherConfigurationValidator :
    IDisposable
{
    private readonly object gate = new();
    private readonly IWeatherService weatherService;
    private CancellationTokenSource? cancellation;
    private string apiKey;
    private string location;
    private long generation;
    private bool started;

    public WeatherConfigurationValidator(IWeatherService weatherService,
        GlanceModuleOptions<WeatherSettings> options)
    {
        this.weatherService = weatherService;
        apiKey = options.Current.ApiKey;
        location = options.Current.Location;
    }

    internal WeatherConfigurationValidation Current { get; private set; } = new(WeatherConfigurationError.None,
        WeatherConfigurationError.None);

    internal event EventHandler? Changed;

    internal void Update(WeatherSettings settings) => Update(settings.ApiKey, settings.Location);

    internal void UpdateApiKey(string value) => Update(value, location);

    internal void UpdateLocation(string value) => Update(apiKey, value);

    public void Dispose()
    {
        lock (gate)
        {
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = null;
        }
    }

    private void Update(string updatedApiKey,
        string updatedLocation)
    {
        updatedApiKey = updatedApiKey.Trim();
        updatedLocation = updatedLocation.Trim();
        CancellationToken cancellationToken;
        long requestedGeneration;

        lock (gate)
        {
            if (started &&
                string.Equals(apiKey, updatedApiKey, StringComparison.Ordinal) &&
                string.Equals(location, updatedLocation, StringComparison.Ordinal))
            {
                return;
            }

            started = true;
            apiKey = updatedApiKey;
            location = updatedLocation;
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = new CancellationTokenSource();
            generation++;
            requestedGeneration = generation;
            cancellationToken = cancellation.Token;
        }

        WeatherConfigurationValidation validation = ValidateLocally(updatedApiKey, updatedLocation);
        Publish(validation);

        if (validation.ApiKeyError == WeatherConfigurationError.None &&
            validation.LocationError == WeatherConfigurationError.None)
        {
            _ = ValidateRemotelyAsync(updatedApiKey, updatedLocation, requestedGeneration, cancellationToken);
        }
    }

    private async Task ValidateRemotelyAsync(string apiKey,
        string location,
        long requestedGeneration,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(700), cancellationToken);
            WeatherSettings settings = new()
            {
                ApiKey = apiKey,
                Location = location
            };
            _ = await weatherService.GetCurrentAsync(settings, cancellationToken);
            PublishIfCurrent(new WeatherConfigurationValidation(WeatherConfigurationError.None,
                WeatherConfigurationError.None), requestedGeneration, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            PublishIfCurrent(new WeatherConfigurationValidation(WeatherConfigurationError.Rejected,
                WeatherConfigurationError.None), requestedGeneration, cancellationToken);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            PublishIfCurrent(new WeatherConfigurationValidation(WeatherConfigurationError.None,
                WeatherConfigurationError.NotFound), requestedGeneration, cancellationToken);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.BadRequest)
        {
            PublishIfCurrent(new WeatherConfigurationValidation(WeatherConfigurationError.None,
                WeatherConfigurationError.Invalid), requestedGeneration, cancellationToken);
        }
        catch (HttpRequestException exception) when ((int?)exception.StatusCode == 429)
        {
            PublishIfCurrent(new WeatherConfigurationValidation(WeatherConfigurationError.RateLimited,
                WeatherConfigurationError.None), requestedGeneration, cancellationToken);
        }
        catch (HttpRequestException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void PublishIfCurrent(WeatherConfigurationValidation validation,
        long requestedGeneration,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (cancellationToken.IsCancellationRequested || requestedGeneration != generation)
            {
                return;
            }
        }

        Publish(validation);
    }

    private void Publish(WeatherConfigurationValidation validation)
    {
        Current = validation;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static WeatherConfigurationValidation ValidateLocally(string apiKey,
        string location)
    {
        WeatherConfigurationError apiKeyError = string.IsNullOrWhiteSpace(apiKey)
            ? WeatherConfigurationError.Required
            : apiKey.Length != 32 || apiKey.Any(character => !char.IsAsciiHexDigit(character))
                ? WeatherConfigurationError.Invalid
                : WeatherConfigurationError.None;
        WeatherConfigurationError locationError = string.IsNullOrWhiteSpace(location)
            ? WeatherConfigurationError.Required
            : location.Length > 100
                ? WeatherConfigurationError.Invalid
                : WeatherConfigurationError.None;
        return new WeatherConfigurationValidation(apiKeyError, locationError);
    }
}

internal sealed record WeatherConfigurationValidation(WeatherConfigurationError ApiKeyError,
    WeatherConfigurationError LocationError);

internal enum WeatherConfigurationError
{
    None,
    Required,
    Invalid,
    Rejected,
    NotFound,
    RateLimited
}
