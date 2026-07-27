namespace Glance.ThemeSwitcher;

public sealed record ThemeChangeResult(bool Succeeded,
    ThemeVariant EffectiveTheme,
    DateTimeOffset? NextChange,
    double? Latitude = null,
    double? Longitude = null,
    string? ErrorKey = null);
