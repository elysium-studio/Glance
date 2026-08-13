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

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterComponentsAsync([new TestComponent("timer")], () => []));
    }

    [Fact]
    public void UnavailableComponentIsExcludedUntilItBecomesAvailable()
    {
        GlanceSettings settings = new();
        TestAvailabilityComponent component = new("Infinity");
        ModulePreferenceService service = new([component], settings, new TestWritableOptions(settings));
        int activeComponentsChanged = 0;
        service.ActiveComponentsChanged += (_, _) => activeComponentsChanged++;

        Assert.Empty(service.GetActiveComponents());
        Assert.True(service.IsEnabled(component.Id));

        component.SetAvailable(true);

        Assert.Equal(component, Assert.Single(service.GetActiveComponents()));
        Assert.Equal(1, activeComponentsChanged);

        component.SetAvailable(false);

        Assert.Empty(service.GetActiveComponents());
        Assert.Equal(2, activeComponentsChanged);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AttentionUsesTheComponentsDefaultUntilTheUserChooses(bool isEnabledByDefault)
    {
        GlanceSettings settings = new();
        TestAttentionComponent component = new("Timer", isEnabledByDefault);
        ModulePreferenceService service = new([component], settings, new TestWritableOptions(settings));

        Assert.Equal(isEnabledByDefault, service.IsAttentionEnabled(component.Id));
    }

    [Fact]
    public async Task AttentionPreferenceOverridesTheComponentDefaultAndIsPersisted()
    {
        GlanceSettings settings = new();
        TestWritableOptions writer = new(settings);
        TestAttentionComponent component = new("Media", true);
        ModulePreferenceService service = new([component], settings, writer);

        await service.SetAttentionEnabledAsync(component.Id, false);

        Assert.False(service.IsAttentionEnabled(component.Id));
        Assert.False(Assert.Single(service.GetPreferences()).IsAttentionEnabled);
        Assert.False(Assert.Single(settings.Modules).IsAttentionEnabled);
        Assert.Equal(1, writer.WriteCount);
    }

    [Fact]
    public void ComponentsWithoutAttentionCapabilityCannotRequestAttention()
    {
        GlanceSettings settings = new();
        TestComponent component = new("Stopwatch");
        ModulePreferenceService service = new([component], settings, new TestWritableOptions(settings));

        Assert.False(service.IsAttentionEnabled(component.Id));
    }

    [Fact]
    public async Task ModuleOrderIsPersistedAndUsedForActiveComponents()
    {
        GlanceSettings settings = new();
        TestWritableOptions writer = new(settings);
        TestComponent clipboard = new("Clipboard");
        TestComponent media = new("Media");
        TestComponent weather = new("Weather");
        ModulePreferenceService service = new([clipboard, media, weather], settings, writer);

        await service.SetOrderAsync([weather.Id, clipboard.Id, media.Id]);

        Assert.Equal([weather.Id, clipboard.Id, media.Id], service.GetPreferences().Select(item => item.Id));
        Assert.Equal([weather, clipboard, media], service.GetActiveComponents());
        Assert.Equal(1, writer.WriteCount);
    }

    [Fact]
    public async Task ReorderingLoadedModulesPreservesAnUnloadedModulesPosition()
    {
        GlanceSettings settings = new()
        {
            Modules =
            [
                new GlanceModulePreference { Id = "Clipboard" },
                new GlanceModulePreference { Id = "ThirdParty" },
                new GlanceModulePreference { Id = "Weather" }
            ]
        };
        TestWritableOptions writer = new(settings);
        ModulePreferenceService service = new([new TestComponent("Clipboard"), new TestComponent("Weather")], settings, writer);

        await service.SetOrderAsync(["Weather", "Clipboard"]);

        Assert.Equal(["Weather", "ThirdParty", "Clipboard"], settings.Modules.Select(item => item.Id));
        Assert.Equal(1, writer.WriteCount);
    }

    [Fact]
    public async Task TransientComponentCanBeConfiguredButIsNeverPagedOrOrdered()
    {
        GlanceSettings settings = new();
        TestWritableOptions writer = new(settings);
        TestComponent weather = new("Weather");
        TestTransientComponent indicators = new("SystemIndicators");
        ModulePreferenceService service = new([weather, indicators], settings, writer);

        Assert.Equal(weather, Assert.Single(service.GetActiveComponents()));
        Assert.Equal(indicators, Assert.Single(service.GetTransientComponents()));
        Assert.Equal(["Weather", "SystemIndicators"], service.GetPreferences().Select(item => item.Id));
        Assert.True(indicators.IsPresentationEnabled);

        Assert.True(await service.SetEnabledAsync(indicators.Id, false));
        Assert.False(service.IsEnabled(indicators.Id));
        Assert.False(indicators.IsPresentationEnabled);
        Assert.Equal(weather, Assert.Single(service.GetActiveComponents()));
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

    private sealed class TestAvailabilityComponent(string id) :
        IGlanceComponent,
        IGlanceAvailabilityComponent
    {
        public string Id { get; } = id;

        public string DisplayName => Id;

        public string Description => string.Empty;

        public int Order => 0;

        public object CompactContent { get; } = new();

        public object ExpandedContent { get; } = new();

        public bool IsAvailable { get; private set; }

        public event EventHandler? AvailabilityChanged;

        public void SetAvailable(bool value)
        {
            if (IsAvailable == value)
            {
                return;
            }

            IsAvailable = value;
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class TestTransientComponent(string id) :
        IGlanceTransientComponent
    {
        public event EventHandler<GlanceTransientPresentationRequestedEventArgs>? PresentationRequested;

        public event EventHandler? DismissalRequested;

        public bool IsPresentationEnabled { get; set; }

        public string Id { get; } = id;

        public string DisplayName => Id;

        public string Description => string.Empty;

        public int Order => 0;

        public object CompactContent { get; } = new();

        public object ExpandedContent { get; } = new();

        public void Present() => PresentationRequested?.Invoke(this, new GlanceTransientPresentationRequestedEventArgs());

        public void Dismiss() => DismissalRequested?.Invoke(this, EventArgs.Empty);
    }

    private sealed class TestAttentionComponent(string id, bool isAttentionEnabledByDefault) :
        IGlanceComponent,
        IGlanceAttentionComponent
    {
        public string Id { get; } = id;

        public string DisplayName => Id;

        public string Description => string.Empty;

        public int Order => 0;

        public object CompactContent { get; } = new();

        public object ExpandedContent { get; } = new();

        public bool IsAttentionEnabledByDefault { get; } = isAttentionEnabledByDefault;
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
