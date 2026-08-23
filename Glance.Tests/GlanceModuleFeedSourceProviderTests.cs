using Glance.Shell;
using Xunit;

namespace Glance.Tests;

public sealed class GlanceModuleFeedSourceProviderTests
{
    [Fact]
    public void ReturnsBuiltInAndCustomFeedsInPriorityOrder()
    {
        GlanceSettings settings = new()
        {
            ModuleFeeds =
            [
                new GlanceModuleFeedPreference { Id = "official", DisplayName = "Official", Url = "https://ignored.example/index.json", IsEnabled = false, IsBuiltIn = true },
                new GlanceModuleFeedPreference { Id = "custom-one", DisplayName = "Custom", Url = "https://modules.example/index.json", IsEnabled = true, IsBuiltIn = false }
            ]
        };
        GlanceModuleFeedDefinition definition = new("official", "Official", new Uri("https://official.example/index.json"), true, false, 100);
        GlanceModuleFeedSourceProvider provider = new(settings, [definition]);

        IReadOnlyList<GlanceModuleFeedSource> sources = provider.GetSources();

        Assert.Collection(sources,
            source =>
            {
                Assert.Equal("official", source.Id);
                Assert.False(source.IsEnabled);
                Assert.Equal(definition.Uri, source.Uri);
                Assert.True(source.IsBuiltIn);
            },
            source =>
            {
                Assert.Equal("custom-one", source.Id);
                Assert.True(source.IsEnabled);
                Assert.Equal(new Uri("https://modules.example/index.json"), source.Uri);
                Assert.False(source.IsBuiltIn);
            });
    }

    [Fact]
    public void UsesLastPreferenceWhenAFeedHasStoredDuplicates()
    {
        GlanceSettings settings = new()
        {
            ModuleFeeds =
            [
                new GlanceModuleFeedPreference { Id = "official", DisplayName = "Official", Url = "https://official.example/index.json", IsEnabled = false, IsBuiltIn = true },
                new GlanceModuleFeedPreference { Id = "official", DisplayName = "Official", Url = "https://official.example/index.json", IsEnabled = true, IsBuiltIn = true }
            ]
        };
        GlanceModuleFeedDefinition definition = new("official", "Official", new Uri("https://official.example/index.json"), false, false, 100);
        GlanceModuleFeedSourceProvider provider = new(settings, [definition]);

        GlanceModuleFeedSource source = Assert.Single(provider.GetSources());

        Assert.True(source.IsEnabled);
    }
}
