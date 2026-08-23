namespace Glance.Shell;

public sealed class GlanceModuleFeedPreference
{
    public required string Id { get; set; }

    public required string DisplayName { get; set; }

    public required string Url { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsBuiltIn { get; set; }
}
