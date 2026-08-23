namespace Glance.Shell;

public interface IGlanceModuleFeedSourceProvider
{
    IReadOnlyList<GlanceModuleFeedSource> GetSources();
}
