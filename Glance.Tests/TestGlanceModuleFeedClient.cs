using Glance.Shell;

namespace Glance.Tests;

internal sealed class TestGlanceModuleFeedClient :
    IGlanceModuleFeedClient
{
    public Dictionary<string, GlanceModuleFeed> Feeds { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> FailedFeeds { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<GlanceModuleFeed?> GetAsync(GlanceModuleFeedSource source, CancellationToken cancellationToken = default) => FailedFeeds.Contains(source.Id) ? throw new HttpRequestException("Unavailable") : Task.FromResult(Feeds.GetValueOrDefault(source.Id));
}
