namespace Glance.Shell;

public sealed class GlanceModuleFeed
{
    public int SchemaVersion { get; set; } = 1;

    public required string Channel { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset GeneratedAt { get; set; }

    public List<GlanceModuleFeedItem> Modules { get; set; } = [];
}
