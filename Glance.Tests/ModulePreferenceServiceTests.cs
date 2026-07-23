using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.Shell;
using Xunit;

namespace Glance.Tests;

public sealed class ModulePreferenceServiceTests
{
    [Fact]
    public async Task RegisterComponentsAddsAHotLoadedComponentAndPersistsItsPreference()
    {
        GlanceSettings settings = new();
        TestWritableOptions writer = new(settings);
        ModulePreferenceService service = new([], settings, writer);
        TestComponent component = new("Weather");
        int preferencesChanged = 0;
        int componentsAdded = 0;
        service.PreferencesChanged += (_, _) => preferencesChanged++;
        service.ComponentsAdded += (_, _) => componentsAdded++;

        await service.RegisterComponentsAsync([component], () => []);

        Assert.Equal(component, Assert.Single(service.GetActiveComponents()));
        Assert.Equal("Weather", Assert.Single(service.GetPreferences()).Id);
        Assert.Equal(1, preferencesChanged);
        Assert.Equal(1, componentsAdded);
        Assert.Equal(1, writer.WriteCount);
    }

    [Fact]
    public async Task SavedPreferenceIsRetainedUntilItsModuleIsLoaded()
    {
        GlanceSettings settings = new()
        {
            Modules =
            [
                new GlanceModulePreference { Id = "Weather", IsEnabled = false },
                new GlanceModulePreference { Id = "Timer" }
            ]
        };
        TestWritableOptions writer = new(settings);
        TestComponent timer = new("Timer");
        ModulePreferenceService service = new([timer], settings, writer);

        Assert.Equal("Timer", Assert.Single(service.GetPreferences()).Id);

        await service.RegisterComponentsAsync([new TestComponent("Weather")], () => []);

        Assert.Equal(["Weather", "Timer"], service.GetPreferences().Select(item => item.Id));
        Assert.False(service.GetPreferences()[0].IsEnabled);
        Assert.Equal(timer, Assert.Single(service.GetActiveComponents()));
        Assert.Equal(0, writer.WriteCount);
    }

    [Fact]
    public async Task DuplicateComponentIdentifierIsRejected()
    {
        GlanceSettings settings = new();
        ModulePreferenceService service = new([new TestComponent("Timer")], settings, new TestWritableOptions(settings));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterComponentsAsync([new TestComponent("timer")], () => []));
    }

    private sealed class TestComponent(string id) :
        IGlanceComponent
    {
        public string Id { get; } = id;

        public string DisplayName => Id;

        public string Description => string.Empty;

        public int Order => 0;

        public object CompactContent { get; } = new();

        public object ExpandedContent { get; } = new();
    }

    private sealed class TestWritableOptions(GlanceSettings settings) :
        IWritableOptions<GlanceSettings>
    {
        public int WriteCount { get; private set; }

        public Task<GlanceSettings?> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<GlanceSettings?>(settings);

        public Task WriteAsync(
            Action<GlanceSettings> update,
            CancellationToken cancellationToken = default)
        {
            update(settings);
            WriteCount++;
            return Task.CompletedTask;
        }

        public Task WriteAsync(
            GlanceSettings value,
            CancellationToken cancellationToken = default)
        {
            WriteCount++;
            return Task.CompletedTask;
        }
    }
}
