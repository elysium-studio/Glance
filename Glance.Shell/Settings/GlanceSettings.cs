namespace Glance.Shell;

public sealed class GlanceSettings
{
    public List<GlanceModulePreference> Modules { get; set; } = [];

    public GlancePlacement Placement { get; set; } = GlancePlacement.Top;

    public bool StartWithWindows { get; set; } = true;
}
