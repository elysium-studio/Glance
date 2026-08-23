using Glance.Shell;

namespace Glance.Tests;

internal sealed class TestGlanceModuleFeedCache :
    IGlanceModuleFeedCache
{
    public Dictionary<string, GlanceModuleFeed> Feeds { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool FailWrites { get; set; }

    public Task<GlanceModuleFeed?> ReadAsync(GlanceModuleFeedSource source, CancellationToken cancellationToken = default) => Task.FromResult(Feeds.GetValueOrDefault(source.Id));

    public Task WriteAsync(GlanceModuleFeedSource source, GlanceModuleFeed feed, CancellationToken cancellationToken = default)
    {
        if (FailWrites)
        {
            throw new IOException("Cache write failed");
        }

        Feeds[source.Id] = feed;
        return Task.CompletedTask;
    }
}
