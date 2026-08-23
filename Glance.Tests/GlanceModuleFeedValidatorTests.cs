using Glance.Shell;
using System.Text.Json;
using Xunit;

namespace Glance.Tests;

public sealed class GlanceModuleFeedValidatorTests
{
    [Fact]
    public void ProductionFeedRejectsLocalPackages()
    {
        GlanceModuleFeedSource source = new("official", "Official", new Uri("https://example.com/index.json"), true, true, false, 100);
        GlanceModuleFeed feed = CreateFeed(new Uri("file:///C:/modules/Weather.glance"));

        Assert.Throws<InvalidDataException>(() => new GlanceModuleFeedValidator().Validate(feed, source));
    }

    [Fact]
    public void DevelopmentFeedAcceptsContainedLocalPackages()
    {
        string root = Path.Combine(Path.GetTempPath(), "GlanceFeedTests", Guid.NewGuid().ToString("N"));
        GlanceModuleFeedSource source = new("local", "Local", new Uri(Path.Combine(root, "index.json")), true, true, true, 0);
        GlanceModuleFeed feed = CreateFeed(new Uri(Path.Combine(root, "Modules", "Weather.glance")));

        new GlanceModuleFeedValidator().Validate(feed, source);
    }

    [Fact]
    public void ProductionFeedAcceptsHttpsIconsAndAccentColors()
    {
        GlanceModuleFeedSource source = new("official", "Official", new Uri("https://example.com/index.json"), true, true, false, 100);
        GlanceModuleFeed feed = CreateFeed(new Uri("https://example.com/modules/Weather.glance"));
        feed.Modules[0].Icon = new GlanceModuleFeedIcon
        {
            Type = GlanceModuleIconType.Bitmap,
            Source = "https://example.com/icons/Weather.png",
            LightSource = "https://example.com/icons/Weather.Light.png",
            AccentColor = "#FF60A5FA",
            LightAccentColor = "#2563A8"
        };

        new GlanceModuleFeedValidator().Validate(feed, source);
    }

    [Fact]
    public void ProductionFeedAcceptsPathIcon()
    {
        GlanceModuleFeedSource source = new("official", "Official", new Uri("https://example.com/index.json"), true, true, false, 100);
        GlanceModuleFeed feed = CreateFeed(new Uri("https://example.com/modules/Weather.glance"));
        feed.Modules[0].Icon = new GlanceModuleFeedIcon
        {
            Type = GlanceModuleIconType.Path,
            Source = "M 0,0 L 16,0 16,16 0,16 Z",
            LightSource = "M 1,1 L 15,1 15,15 1,15 Z",
            AccentColor = "#FF60A5FA"
        };

        new GlanceModuleFeedValidator().Validate(feed, source);
    }

    [Fact]
    public void ProductionFeedRejectsInvalidVisualMetadata()
    {
        GlanceModuleFeedSource source = new("official", "Official", new Uri("https://example.com/index.json"), true, true, false, 100);
        GlanceModuleFeed feed = CreateFeed(new Uri("https://example.com/modules/Weather.glance"));
        feed.Modules[0].Icon = new GlanceModuleFeedIcon
        {
            Type = GlanceModuleIconType.Bitmap,
            Source = "http://example.com/icons/Weather.png",
            AccentColor = "blue"
        };

        Assert.Throws<InvalidDataException>(() => new GlanceModuleFeedValidator().Validate(feed, source));
    }

    [Fact]
    public void IconUsesDiscriminatedFeedMetadata()
    {
        GlanceModuleFeed feed = CreateFeed(new Uri("https://example.com/modules/Weather.glance"));
        string json = JsonSerializer.Serialize(feed, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        GlanceModuleFeed result = JsonSerializer.Deserialize<GlanceModuleFeed>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Contains("\"icon\":{\"type\":\"glyph\",\"source\":", json);
        Assert.Equal(GlanceModuleIconType.Glyph, result.Modules[0].Icon.Type);
        Assert.Equal("\uE9CA", result.Modules[0].Icon.Source);
    }

    private static GlanceModuleFeed CreateFeed(Uri packageUri) => new()
    {
        Channel = "stable",
        GeneratedAt = DateTimeOffset.UtcNow,
        Modules =
        [
            new GlanceModuleFeedItem
            {
                Id = "Weather",
                Version = "1.0.0",
                ModuleApiVersion = 1,
                DisplayName = "Weather",
                Description = "Weather",
                Category = "Information",
                Icon = new GlanceModuleFeedIcon
                {
                    Type = GlanceModuleIconType.Glyph,
                    Source = "\uE9CA",
                    FontFamily = "Segoe Fluent Icons"
                },
                DownloadUrl = packageUri,
                Sha256 = new string('A', 64),
                Size = 1
            }
        ]
    };
}
