namespace Glance.Shell;

public sealed class GlanceModuleFeedIcon
{
    public GlanceModuleIconType Type { get; set; }

    public required string Source { get; set; }

    public string LightSource { get; set; } = string.Empty;

    public string FontFamily { get; set; } = string.Empty;

    public string AccentColor { get; set; } = string.Empty;

    public string LightAccentColor { get; set; } = string.Empty;
}
