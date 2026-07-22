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
        ModuleSettingsItemViewModel item = new("Timer", "Timer", "Countdown", true, [first, second], (_, _) => Task.FromResult(true));

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
        ModuleSettingsItemViewModel item = new("Stopwatch", "Stopwatch", "Elapsed time", true, [], (_, _) => Task.FromResult(true));

        Assert.False(item.CanExpand);
    }

    [Fact]
    public void DisposeDisposesOwnedSettingViewModels()
    {
        TestSetting setting = new("Timer", 10);
        ModuleSettingsItemViewModel item = new("Timer", "Timer", "Countdown", false, [setting], (_, _) => Task.FromResult(true));

        item.Dispose();

        Assert.True(setting.IsDisposed);
    }

    private sealed class TestSetting(string moduleId, int order) : IGlanceModuleSettingViewModel
    {
        public string ModuleId { get; } = moduleId;

        public int Order { get; } = order;

        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
