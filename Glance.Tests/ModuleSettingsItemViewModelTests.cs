using Glance.Application.Abstractions;
using Glance.Shell;
using Xunit;

namespace Glance.Tests;

public sealed class ModuleSettingsItemViewModelTests
{
    [Fact]
    public void EnabledStateControlsExposedModuleSettings()
    {
        TestSetting first = new("Timer", 10);
        TestSetting second = new("Timer", 20);
        ModuleSettingsItemViewModel item = new("Timer", "Timer", "Countdown", new TestComponent("Timer"), true, [first, second], new ImmediateTestDispatcher(), new TestNavigator(), _ => { }, (_, _) => Task.FromResult(true));

        _ = Assert.IsAssignableFrom<ISettingViewModel>(item.Settings);
        Assert.Equal([first, second], item.Settings);

        item.IsEnabled = false;
        Assert.Empty(item.Settings);
        Assert.False(item.CanExpand);

        item.IsEnabled = true;
        Assert.Equal([first, second], item.Settings);
        Assert.True(item.CanExpand);
    }

    [Fact]
    public void ModuleWithoutSettingsCannotExpand()
    {
        ModuleSettingsItemViewModel item = new("Stopwatch", "Stopwatch", "Elapsed time", new TestComponent("Stopwatch"), true, [], new ImmediateTestDispatcher(), new TestNavigator(), _ => { }, (_, _) => Task.FromResult(true));

        Assert.False(item.CanExpand);
    }

    [Fact]
    public void EnabledModuleWithSettingsCanRequestNavigation()
    {
        int navigationRequests = 0;
        TestSetting setting = new("Timer", 10);
        ModuleSettingsItemViewModel item = new("Timer", "Timer", "Countdown", new TestComponent("Timer"), true, [setting], new ImmediateTestDispatcher(), new TestNavigator(), _ => navigationRequests++, (_, _) => Task.FromResult(true));

        item.NavigateToSettings();
        item.IsEnabled = false;
        item.NavigateToSettings();

        Assert.Equal(1, navigationRequests);
    }

    [Fact]
    public void DisposeDisposesOwnedSettingViewModels()
    {
        TestSetting setting = new("Timer", 10);
        ModuleSettingsItemViewModel item = new("Timer", "Timer", "Countdown", new TestComponent("Timer"), false, [setting], new ImmediateTestDispatcher(), new TestNavigator(), _ => { }, (_, _) => Task.FromResult(true));

        item.Dispose();
        item.Dispose();

        Assert.Equal(1, setting.DisposeCount);
    }

    [Fact]
    public void RequiredUpdateHidesInstalledModuleActions()
    {
        int navigationRequests = 0;
        TestSetting setting = new("WorldClock", 10);
        ModuleSettingsItemViewModel item = new("WorldClock", "World clock", "Local time", new TestComponent("WorldClock"), true, [setting], new ImmediateTestDispatcher(), new TestNavigator(), _ => navigationRequests++, (_, _) => Task.FromResult(true), _ => Task.FromResult(true), _ => Task.FromResult(true));
        GlanceModuleFeedItem update = new()
        {
            Id = "WorldClock",
            Version = "1.0.2",
            ModuleApiVersion = GlanceModuleContract.CurrentVersion,
            DisplayName = "World clock",
            Description = "Local time",
            Category = "Information",
            Icon = new GlanceModuleFeedIcon { Type = GlanceModuleIconType.Glyph, Source = "\uE916" },
            DownloadUrl = new Uri("https://example.com/WorldClock.glance"),
            Sha256 = string.Empty
        };

        item.SetFeedItem(update, true, "1.0.1");
        item.NavigateToSettings();

        Assert.True(item.ShowUpdateAction);
        Assert.False(item.CanExpand);
        Assert.False(item.CanToggle);
        Assert.False(item.CanUninstall);
        Assert.Equal(0, navigationRequests);
    }

    private sealed class TestSetting(string moduleId, int order) :
        IGlanceModuleSettingViewModel
    {
        public string ModuleId { get; } = moduleId;

        public int Order { get; } = order;

        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
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
}
