namespace Glance.Shell;

public interface IGlanceModuleFeedClient
{
    Task<GlanceModuleFeed?> GetAsync(GlanceModuleFeedSource source, CancellationToken cancellationToken = default);
}
