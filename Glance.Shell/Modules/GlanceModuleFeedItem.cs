using Glance.Application.Abstractions;
using System.Text.Json.Serialization;

namespace Glance.Shell;

public sealed class GlanceModuleFeedItem
{
    public required string Id { get; set; }

    public required string Version { get; set; }

    public int ModuleApiVersion { get; set; }

    public string? MinimumGlanceVersion { get; set; }

    public required string DisplayName { get; set; }

    public required string Description { get; set; }

    public required string Category { get; set; }

    public string CategoryDisplayName { get; set; } = string.Empty;

    public string CategoryGlyph { get; set; } = "\uE74C";

    public int CategoryOrder { get; set; } = 1000;

    public required GlanceModuleFeedIcon Icon { get; set; }

    public int Order { get; set; }

    public required Uri DownloadUrl { get; set; }

    public required string Sha256 { get; set; }

    public long Size { get; set; }

    public bool IsDelisted { get; set; }

    public bool IsRevoked { get; set; }

    public bool IsVisible { get; set; } = true;

    public List<string> Capabilities { get; set; } = [];

    public List<GlanceModuleDependency> Dependencies { get; set; } = [];

    [JsonIgnore]
    public string FeedId { get; set; } = string.Empty;

    public bool IsCompatible => ModuleApiVersion == GlanceModuleContract.CurrentVersion;
}
