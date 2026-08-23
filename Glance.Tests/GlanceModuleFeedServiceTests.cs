using Glance.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Glance.Tests;

public sealed class GlanceModuleFeedServiceTests
{
    [Fact]
    public async Task HigherPriorityFeedWinsDuplicateModuleIds()
    {
        GlanceModuleFeedSource local = new("local", "Local", new Uri("https://local.example/index.json"), true, true, false, 0);
        GlanceModuleFeedSource official = new("official", "Official", new Uri("https://official.example/index.json"), true, true, false, 100);
        TestGlanceModuleFeedClient client = new();
        client.Feeds[local.Id] = CreateFeed("2.0.0");
        client.Feeds[official.Id] = CreateFeed("1.0.0");
        GlanceModuleFeedService service = new(client, new TestGlanceModuleFeedCache(), new TestGlanceModuleFeedSourceProvider(local, official), NullLogger<GlanceModuleFeedService>.Instance);

        await service.RefreshAsync();

        GlanceModuleFeedItem module = Assert.Single(service.Modules);
        Assert.Equal("2.0.0", module.Version);
        Assert.Equal(local.Id, module.FeedId);
    }

    [Fact]
    public async Task FailedFeedUsesItsCacheWithoutHidingAvailableFeeds()
    {
        GlanceModuleFeedSource cached = new("cached", "Cached", new Uri("https://cached.example/index.json"), true, false, false, 100);
        GlanceModuleFeedSource available = new("available", "Available", new Uri("https://available.example/index.json"), true, false, false, 200);
        TestGlanceModuleFeedClient client = new();
        client.FailedFeeds.Add(cached.Id);
        client.Feeds[available.Id] = CreateFeed("2.0.0", "Timer");
        TestGlanceModuleFeedCache cache = new();
        cache.Feeds[cached.Id] = CreateFeed("1.0.0", "Weather");
        GlanceModuleFeedService service = new(client, cache, new TestGlanceModuleFeedSourceProvider(cached, available), NullLogger<GlanceModuleFeedService>.Instance);

        await service.RefreshAsync();

        Assert.True(service.IsAvailable);
        Assert.True(service.IsUsingCache);
        Assert.Equal(2, service.Modules.Count);
        Assert.False(service.IsSourceAvailable(cached.Id));
        Assert.True(service.IsSourceAvailable(available.Id));
    }

    [Fact]
    public async Task CacheWriteFailureDoesNotHideAnAvailableFeed()
    {
        GlanceModuleFeedSource source = new("local", "Local", new Uri("https://local.example/index.json"), true, true, false, 0);
        TestGlanceModuleFeedClient client = new();
        client.Feeds[source.Id] = CreateFeed("1.0.0");
        TestGlanceModuleFeedCache cache = new() { FailWrites = true };
        GlanceModuleFeedService service = new(client, cache, new TestGlanceModuleFeedSourceProvider(source), NullLogger<GlanceModuleFeedService>.Instance);

        await service.RefreshAsync();

        Assert.True(service.IsAvailable);
        Assert.True(service.IsSourceAvailable(source.Id));
        Assert.Single(service.Modules);
    }

    private static GlanceModuleFeed CreateFeed(string version, string id = "Weather") => new()
    {
        Channel = "stable",
        GeneratedAt = DateTimeOffset.UtcNow,
        Modules =
        [
            new GlanceModuleFeedItem
            {
                Id = id,
                Version = version,
                ModuleApiVersion = 1,
                DisplayName = id,
                Description = id,
                Category = "Information",
                Icon = new GlanceModuleFeedIcon
                {
                    Type = GlanceModuleIconType.Glyph,
                    Source = "\uE9CA",
                    FontFamily = "Segoe Fluent Icons"
                },
                DownloadUrl = new Uri($"https://example.com/{id}.glance"),
                Sha256 = new string('A', 64),
                Size = 1
            }
        ]
    };
}
