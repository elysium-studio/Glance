using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.ThemeSwitcher.WinUI;

public sealed class WindowsThemeController(WindowsSystemThemeService systemThemeService,
    WindowsLocationService locationService,
    ILogger<WindowsThemeController> logger) :
    IThemeController
{
    private bool initialized;

    public ThemeVariant CurrentTheme => systemThemeService.CurrentTheme;

    public Task<ThemeChangeResult> RefreshAsync(ThemeSwitcherSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!initialized && settings.Preference != ThemePreference.Sunset)
            {
                initialized = true;
                return Task.FromResult(new ThemeChangeResult(true, CurrentTheme, null));
            }

            ThemeChangeResult result = settings.Preference switch
            {
                ThemePreference.Light => Apply(ThemeVariant.Light),
                ThemePreference.Dark => Apply(ThemeVariant.Dark),
                ThemePreference.Sunset when settings.HasLocation => ApplySolar(settings.Latitude, settings.Longitude, cancellationToken),
                ThemePreference.Sunset => new ThemeChangeResult(false, CurrentTheme, null, ErrorKey: "LocationRequired"),
                _ => new ThemeChangeResult(true, CurrentTheme, null)
            };
            initialized = true;
            return Task.FromResult(result);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to refresh the Windows theme");
            initialized = true;
            return Task.FromResult(new ThemeChangeResult(false, CurrentTheme, null, ErrorKey: "ThemeChangeFailed"));
        }
    }

    public async Task<ThemeChangeResult> SelectAsync(ThemePreference preference,
        ThemeSwitcherSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return preference switch
            {
                ThemePreference.Light => Apply(ThemeVariant.Light),
                ThemePreference.Dark => Apply(ThemeVariant.Dark),
                ThemePreference.Sunset => await SelectSunsetAsync(settings, cancellationToken),
                _ => new ThemeChangeResult(true, CurrentTheme, null)
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to select the Windows theme preference {ThemePreference}", preference);
            return new ThemeChangeResult(false, CurrentTheme, null, ErrorKey: "ThemeChangeFailed");
        }
    }

    private async Task<ThemeChangeResult> SelectSunsetAsync(ThemeSwitcherSettings settings,
        CancellationToken cancellationToken)
    {
        double latitude = settings.Latitude;
        double longitude = settings.Longitude;

        if (!settings.HasLocation)
        {
            (double Latitude, double Longitude)? location = await locationService.GetLocationAsync(cancellationToken);

            if (location is null)
            {
                return new ThemeChangeResult(false, CurrentTheme, null, ErrorKey: "LocationDenied");
            }

            latitude = location.Value.Latitude;
            longitude = location.Value.Longitude;
        }

        ThemeChangeResult result = ApplySolar(latitude, longitude, cancellationToken);
        return result with { Latitude = latitude, Longitude = longitude };
    }

    private ThemeChangeResult ApplySolar(double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        TimeZoneInfo timeZone = TimeZoneInfo.Local;
        SolarSchedule? schedule = SolarCalculator.Calculate(DateOnly.FromDateTime(now.DateTime), latitude, longitude, timeZone);

        if (schedule is null)
        {
            return new ThemeChangeResult(false, CurrentTheme, null, ErrorKey: "SunsetUnavailable");
        }

        ThemeVariant theme;
        DateTimeOffset nextChange;

        if (now < schedule.Sunrise)
        {
            theme = ThemeVariant.Dark;
            nextChange = schedule.Sunrise;
        }
        else if (now < schedule.Sunset)
        {
            theme = ThemeVariant.Light;
            nextChange = schedule.Sunset;
        }
        else
        {
            theme = ThemeVariant.Dark;
            SolarSchedule? tomorrow = SolarCalculator.Calculate(DateOnly.FromDateTime(now.DateTime).AddDays(1), latitude, longitude, timeZone);

            if (tomorrow is null)
            {
                return new ThemeChangeResult(false, CurrentTheme, null, ErrorKey: "SunsetUnavailable");
            }

            nextChange = tomorrow.Sunrise;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Apply(theme, nextChange);
    }

    private ThemeChangeResult Apply(ThemeVariant theme,
        DateTimeOffset? nextChange = null)
    {
        if (CurrentTheme != theme)
        {
            systemThemeService.Apply(theme);
        }

        return new ThemeChangeResult(true, theme, nextChange);
    }
}
