namespace Glance.Application.Abstractions;

public sealed class GlanceModuleDependency
{
    public required string Id { get; set; }

    public required string MinimumVersion { get; set; }
}
