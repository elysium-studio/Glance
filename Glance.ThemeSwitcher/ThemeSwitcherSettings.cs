namespace Glance.ThemeSwitcher;

public sealed class ThemeSwitcherSettings
{
    public bool AnimateTransitions { get; set; } = true;

    public bool HasLocation { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public ThemePreference Preference { get; set; }
}
