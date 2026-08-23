namespace Glance.Shell;

public interface IGlanceModuleDependencyResolver
{
    IReadOnlyList<GlanceModuleFeedItem> Resolve(GlanceModuleFeedItem module);
}
