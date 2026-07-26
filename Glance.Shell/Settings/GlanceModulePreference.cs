namespace Glance.Shell;

public sealed class GlanceModulePreference
{
    public string Id { get; set; } = string.Empty;

    public bool? IsAttentionEnabled { get; set; }

    public bool IsEnabled { get; set; } = true;
}
