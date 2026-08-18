using Elysium.Application.Abstractions;
using Glance.Shell;
using Xunit;

namespace Glance.Tests;

public sealed class GlanceQuickConverterPreferencesTests
{
    [Fact]
    public async Task ConverterPreferencesAreRegisteredUpdatedAndRemoved()
    {
        GlanceSettings settings = new();
        TestWritableOptions writer = new(settings);
        GlanceQuickConverterPreferences preferences = new(settings, writer);

        await preferences.RegisterAsync(["QuickConvert.Images", "QuickConvert.Video"]);

        Assert.Equal(2, settings.Converters.Count);
        Assert.All(settings.Converters, converter => Assert.True(converter.IsEnabled));
        Assert.True(preferences.IsEnabled("QuickConvert.Images"));

        await preferences.SetEnabledAsync("QuickConvert.Images", false);

        Assert.False(preferences.IsEnabled("QuickConvert.Images"));
        Assert.False(settings.Converters.Single(converter => converter.Id == "QuickConvert.Images").IsEnabled);

        await preferences.SetEnabledAsync("QuickConvert.Images", true);

        Assert.True(preferences.IsEnabled("QuickConvert.Images"));
        Assert.True(settings.Converters.Single(converter => converter.Id == "QuickConvert.Images").IsEnabled);

        await preferences.RemoveAsync(["QuickConvert.Video"]);

        Assert.DoesNotContain(settings.Converters, converter => converter.Id == "QuickConvert.Video");
        Assert.True(preferences.IsEnabled("QuickConvert.Video"));
        Assert.Equal(4, writer.WriteCount);
    }

    private sealed class TestWritableOptions(GlanceSettings settings) :
        IWritableOptions<GlanceSettings>
    {
        public int WriteCount { get; private set; }

        public Task<GlanceSettings?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult<GlanceSettings?>(settings);

        public Task WriteAsync(Action<GlanceSettings> update, CancellationToken cancellationToken = default)
        {
            update(settings);
            WriteCount++;
            return Task.CompletedTask;
        }

        public Task WriteAsync(GlanceSettings value, CancellationToken cancellationToken = default)
        {
            WriteCount++;
            return Task.CompletedTask;
        }
    }
}
