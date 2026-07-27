using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using System.Globalization;

namespace Glance.ThemeSwitcher;

public sealed partial class ThemeSwitcherViewModel(IThemeController controller,
    ThemeSwitcherSettings settings,
    ITextLocalizer localizer,
    IDispatcher? dispatcher = null) :
    ObservableObject
{
    private ThemeSwitcherSettings settings = settings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Glyph))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private ThemeVariant effectiveTheme = controller.CurrentTheme;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailText))]
    private DateTimeOffset? nextChange;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailText))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private ThemePreference preference = settings.Preference;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorText;

    public string DetailText
    {
        get
        {
            if (!string.IsNullOrEmpty(ErrorText))
            {
                return ErrorText;
            }

            if (Preference != ThemePreference.Sunset || NextChange is null)
            {
                return localizer.GetText("ManualDetail");
            }

            string time = NextChange.Value.ToString("t", CultureInfo.CurrentCulture);
            return EffectiveTheme == ThemeVariant.Light
                ? localizer.GetText("UntilSunsetDetail", time)
                : localizer.GetText("UntilSunriseDetail", time);
        }
    }

    public string Glyph => EffectiveTheme == ThemeVariant.Light ? "\uE706" : "\uE708";

    public string StatusText => Preference == ThemePreference.Sunset
        ? localizer.GetText("AutomaticStatus")
        : EffectiveTheme == ThemeVariant.Light
            ? localizer.GetText("LightStatus")
            : localizer.GetText("DarkStatus");

    public event EventHandler? SettingsChanged;

    public async Task InitializeAsync()
    {
        ThemeChangeResult result = await controller.RefreshAsync(settings).ConfigureAwait(false);
        Dispatch(() =>
        {
            ThemePreference initialPreference = settings.Preference == ThemePreference.Sunset
                ? ThemePreference.Sunset
                : result.EffectiveTheme == ThemeVariant.Light
                    ? ThemePreference.Light
                    : ThemePreference.Dark;
            settings.Preference = initialPreference;
            ApplyResult(result, initialPreference);
        });
    }

    public Task SelectLightAsync() => SelectAsync(ThemePreference.Light);

    public Task SelectSunsetAsync() => SelectAsync(ThemePreference.Sunset);

    public Task SelectDarkAsync() => SelectAsync(ThemePreference.Dark);

    public async Task RefreshAsync()
    {
        if (IsBusy || Preference != ThemePreference.Sunset)
        {
            return;
        }

        ThemeChangeResult result = await controller.RefreshAsync(settings).ConfigureAwait(false);
        Dispatch(() => ApplyResult(result, Preference));
    }

    public void ApplySettings(ThemeSwitcherSettings updatedSettings)
    {
        settings = updatedSettings;
    }

    public void WriteSettings(ThemeSwitcherSettings target)
    {
        target.HasLocation = settings.HasLocation;
        target.Latitude = settings.Latitude;
        target.Longitude = settings.Longitude;
        target.Preference = Preference;
    }

    private async Task SelectAsync(ThemePreference requestedPreference)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorText = null;

        ThemeChangeResult result;

        try
        {
            result = await controller.SelectAsync(requestedPreference, settings).ConfigureAwait(false);
        }
        catch
        {
            result = new ThemeChangeResult(false, EffectiveTheme, null, ErrorKey: "ThemeChangeFailed");
        }

        Dispatch(() =>
        {
            if (result.Succeeded)
            {
                if (result.Latitude is double latitude && result.Longitude is double longitude)
                {
                    settings.HasLocation = true;
                    settings.Latitude = latitude;
                    settings.Longitude = longitude;
                }

                settings.Preference = requestedPreference;
                ApplyResult(result, requestedPreference);
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ErrorText = localizer.GetText(result.ErrorKey ?? "ThemeChangeFailed");
            }

            IsBusy = false;
        });
    }

    private void ApplyResult(ThemeChangeResult result,
        ThemePreference updatedPreference)
    {
        if (!result.Succeeded)
        {
            ErrorText = localizer.GetText(result.ErrorKey ?? "ThemeChangeFailed");
            return;
        }

        ErrorText = null;
        Preference = updatedPreference;
        EffectiveTheme = result.EffectiveTheme;
        NextChange = result.NextChange;
        OnPropertyChanged(nameof(DetailText));
    }

    private void Dispatch(Action action)
    {
        if (dispatcher is null)
        {
            action();
            return;
        }

        dispatcher.Dispatch(action);
    }
}
