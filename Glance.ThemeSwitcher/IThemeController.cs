namespace Glance.ThemeSwitcher;

public interface IThemeController
{
    ThemeVariant CurrentTheme { get; }

    Task<ThemeChangeResult> RefreshAsync(ThemeSwitcherSettings settings,
        CancellationToken cancellationToken = default);

    Task<ThemeChangeResult> SelectAsync(ThemePreference preference,
        ThemeSwitcherSettings settings,
        CancellationToken cancellationToken = default);
}
