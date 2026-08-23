namespace Glance.Application.Abstractions;

public sealed class GlanceModulePackageManifest
{
    public int SchemaVersion { get; set; } = 1;

    public required string Id { get; set; }

    public required string Version { get; set; }

    public int ModuleApiVersion { get; set; } = GlanceModuleContract.CurrentVersion;

    public string? MinimumGlanceVersion { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string IconGlyph { get; set; } = "\uE8B7";

    public bool IsVisible { get; set; } = true;

    public List<GlanceModuleDependency> Dependencies { get; set; } = [];
}
