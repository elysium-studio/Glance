namespace Glance.Shell;

public sealed class GlanceSettings
{
    public bool AutoHide { get; set; }

    public GlanceExpansionMode ExpansionMode { get; set; }

    public GlanceDisplayLocation DisplayLocation { get; set; }

    public List<GlanceQuickConverterPreference> Converters { get; set; } = [];

    public List<GlanceModulePreference> Modules { get; set; } = [];

    public List<string> UninstalledModulePackages { get; set; } = [];

    public GlancePlacement Placement { get; set; } = GlancePlacement.Top;

    public bool IsAssistantEnabled { get; set; }

    public bool ShowSetupOnStartup { get; set; } = true;

    public bool StartWithWindows { get; set; } = true;

    public string? TranscriptionModelId { get; set; }
}
