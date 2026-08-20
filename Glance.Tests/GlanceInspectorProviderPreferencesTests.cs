using Elysium.Application.Abstractions;
using Glance.Shell;
using Xunit;

namespace Glance.Tests;

public sealed class GlanceInspectorProviderPreferencesTests
{
    [Fact]
    public async Task ProviderPreferencesAreRegisteredUpdatedAndRemoved()
    {
        GlanceSettings settings = new();
        TestWritableOptions writer = new(settings);
        GlanceInspectorProviderPreferences preferences = new(settings, writer);

        await preferences.RegisterAsync(["Inspector.Images", "Inspector.Media"]);

        Assert.Equal(2, settings.InspectorProviders.Count);
        Assert.All(settings.InspectorProviders, provider => Assert.True(provider.IsEnabled));
        Assert.True(preferences.IsEnabled("Inspector.Images"));

        await preferences.SetEnabledAsync("Inspector.Images", false);

        Assert.False(preferences.IsEnabled("Inspector.Images"));
        Assert.False(settings.InspectorProviders.Single(provider => provider.Id == "Inspector.Images").IsEnabled);

        await preferences.SetEnabledAsync("Inspector.Images", true);

        Assert.True(preferences.IsEnabled("Inspector.Images"));

        await preferences.RemoveAsync(["Inspector.Media"]);

        Assert.DoesNotContain(settings.InspectorProviders, provider => provider.Id == "Inspector.Media");
        Assert.True(preferences.IsEnabled("Inspector.Media"));
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
