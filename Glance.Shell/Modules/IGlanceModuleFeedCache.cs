namespace Glance.Shell;

public interface IGlanceModuleFeedCache
{
    Task<GlanceModuleFeed?> ReadAsync(GlanceModuleFeedSource source, CancellationToken cancellationToken = default);

    Task WriteAsync(GlanceModuleFeedSource source, GlanceModuleFeed feed, CancellationToken cancellationToken = default);
}
