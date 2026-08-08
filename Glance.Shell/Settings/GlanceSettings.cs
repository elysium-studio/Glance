namespace Glance.Shell;

public sealed class GlanceSettings
{
    public string? AssistantProviderId { get; set; }

    public string? AssistantSemanticResolverId { get; set; }

    public bool AutoHide { get; set; }

    public GlanceExpansionMode ExpansionMode { get; set; }

    public List<GlanceModulePreference> Modules { get; set; } = [];

    public GlancePlacement Placement { get; set; } = GlancePlacement.Top;

    public bool IsAssistantEnabled { get; set; } = true;

    public bool ShowSetupOnStartup { get; set; } = true;

    public bool StartWithWindows { get; set; } = true;
}
