using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed class GlanceModuleDependencyResolver :
    IGlanceModuleDependencyResolver
{
    private readonly IGlanceModuleFeedService feed;
    private readonly ModuleInstallationService installations;

    public GlanceModuleDependencyResolver(IGlanceModuleFeedService feed, ModuleInstallationService installations)
    {
        this.feed = feed;
        this.installations = installations;
    }

    public IReadOnlyList<GlanceModuleFeedItem> Resolve(GlanceModuleFeedItem module)
    {
        List<GlanceModuleFeedItem> resolved = [];
        HashSet<string> completed = [with(StringComparer.OrdinalIgnoreCase)];
        HashSet<string> visiting = [with(StringComparer.OrdinalIgnoreCase)];

        foreach (GlanceModuleDependency dependency in module.Dependencies)
        {
            Resolve(dependency, module.FeedId, resolved, completed, visiting);
        }

        return resolved;
    }

    private void Resolve(GlanceModuleDependency dependency, string feedId, List<GlanceModuleFeedItem> resolved, HashSet<string> completed, HashSet<string> visiting)
    {
        if (IsInstalled(dependency) || completed.Contains(dependency.Id))
        {
            return;
        }

        if (!visiting.Add(dependency.Id))
        {
            throw new InvalidDataException("The module has an invalid dependency chain.");
        }

        GlanceModuleFeedItem module = feed.Modules.FirstOrDefault(candidate => string.Equals(candidate.Id, dependency.Id, StringComparison.OrdinalIgnoreCase) && string.Equals(candidate.FeedId, feedId, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidDataException("A required part of this module is not available from the same feed.");

        foreach (GlanceModuleDependency child in module.Dependencies)
        {
            Resolve(child, feedId, resolved, completed, visiting);
        }

        _ = visiting.Remove(dependency.Id);
        _ = completed.Add(dependency.Id);
        resolved.Add(module);
    }

    private bool IsInstalled(GlanceModuleDependency dependency) => Version.TryParse(installations.GetInstalledVersion(dependency.Id), out Version? installed) && Version.TryParse(dependency.MinimumVersion, out Version? minimum) && installed >= minimum;
}
