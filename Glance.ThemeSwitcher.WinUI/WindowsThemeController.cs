using System;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.ThemeSwitcher.WinUI;

public sealed class WindowsThemeController(WindowsSystemThemeService systemThemeService,
    WindowsLocationService locationService,
    ThemeTransitionService transitionService) :
    IThemeController
{
    private bool initialized;

    public ThemeVariant CurrentTheme => systemThemeService.CurrentTheme;

    public async Task<ThemeChangeResult> RefreshAsync(ThemeSwitcherSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!initialized && settings.Preference != ThemePreference.Sunset)
            {
                initialized = true;
                return new ThemeChangeResult(true, CurrentTheme, null);
            }

            ThemeChangeResult result = settings.Preference switch
            {
                ThemePreference.Light => await ApplyAsync(ThemeVariant.Light, null, initialized && settings.AnimateTransitions, cancellationToken),
                ThemePreference.Dark => await ApplyAsync(ThemeVariant.Dark, null, initialized && settings.AnimateTransitions, cancellationToken),
                ThemePreference.Sunset when settings.HasLocation => await ApplySolarAsync(settings.Latitude, settings.Longitude, initialized && settings.AnimateTransitions, cancellationToken),
                ThemePreference.Sunset => new ThemeChangeResult(false, CurrentTheme, null, ErrorKey: "LocationRequired"),
                _ => new ThemeChangeResult(true, CurrentTheme, null)
            };
            initialized = true;
            return result;
        }
        catch
        {
            initialized = true;
            return new ThemeChangeResult(false, CurrentTheme, null, ErrorKey: "ThemeChangeFailed");
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
                ThemePreference.Light => await ApplyAsync(ThemeVariant.Light, null, settings.AnimateTransitions, cancellationToken),
                ThemePreference.Dark => await ApplyAsync(ThemeVariant.Dark, null, settings.AnimateTransitions, cancellationToken),
                ThemePreference.Sunset => await SelectSunsetAsync(settings, cancellationToken),
                _ => new ThemeChangeResult(true, CurrentTheme, null)
            };
        }
        catch
        {
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

        ThemeChangeResult result = await ApplySolarAsync(latitude, longitude, settings.AnimateTransitions, cancellationToken);
        return result with { Latitude = latitude, Longitude = longitude };
    }

    private async Task<ThemeChangeResult> ApplySolarAsync(double latitude,
        double longitude,
        bool animate,
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

        return await ApplyAsync(theme, nextChange, animate, cancellationToken);
    }

    private async Task<ThemeChangeResult> ApplyAsync(ThemeVariant theme,
        DateTimeOffset? nextChange,
        bool animate,
        CancellationToken cancellationToken)
    {
        if (CurrentTheme != theme)
        {
            if (animate)
            {
                await transitionService.PlayAsync(theme, () =>
                {
                    systemThemeService.Apply(theme);
                    return Task.CompletedTask;
                }, cancellationToken);
            }
            else
            {
                systemThemeService.Apply(theme);
            }
        }

        return new ThemeChangeResult(true, theme, nextChange);
    }
}
