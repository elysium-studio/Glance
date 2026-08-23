using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Glance.Tests;

public sealed class SetupTourViewModelTests
{
    [Fact]
    public void NavigationAndSelectionStateRemainConsistent()
    {
        GlanceSettings settings = new();
        TestWritableOptions writer = new(settings);
        ModulePreferenceService preferences = new([new TestComponent("Weather")], settings, writer);
        SetupTourViewModel viewModel = CreateViewModel(settings, preferences, writer);

        Assert.True(viewModel.IsCompactModeSelected);
        Assert.True(viewModel.IsAlwaysVisibleSelected);
        Assert.True(viewModel.IsTopPlacementSelected);
        Assert.False(viewModel.CanGoBack);
        Assert.True(viewModel.CanGoNext);

        viewModel.GoNext();
        viewModel.GoNext();
        viewModel.GoNext();
        viewModel.GoNext();

        Assert.True(viewModel.IsLastPage);
        Assert.False(viewModel.CanGoNext);

        viewModel.GoBack();

        Assert.Equal(3, viewModel.CurrentPage);
        Assert.True(viewModel.CanGoBack);
    }

    [Fact]
    public async Task ChoicesApplyImmediatelyAndFinishDisablesTheTour()
    {
        GlanceSettings settings = new();
        TestWritableOptions writer = new(settings);
        TestComponent weather = new("Weather");
        TestComponent timer = new("Timer");
        ModulePreferenceService preferences = new([weather, timer], settings, writer);
        ModuleInstallationService installations = new();
        installations.Register("Timer", "1.0.0", ["Timer"], async () =>
        {
            await preferences.UnregisterComponentsAsync([timer]);
            return true;
        });
        SetupTourViewModel viewModel = CreateViewModel(settings, preferences, writer, installations: installations);
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.Finished += (_, _) => completion.SetResult();
        await viewModel.SelectExpansionModeAsync(GlanceExpansionMode.AlwaysExpanded);
        await viewModel.SelectAutoHideAsync(true);
        await viewModel.SelectPlacementAsync(GlancePlacement.Bottom);
        await viewModel.Modules[1].RemoveAsync();

        Assert.Equal(GlanceExpansionMode.AlwaysExpanded, settings.ExpansionMode);
        Assert.True(settings.AutoHide);
        Assert.Equal(GlancePlacement.Bottom, settings.Placement);
        Assert.True(settings.ShowSetupOnStartup);

        viewModel.Finish();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(GlanceExpansionMode.AlwaysExpanded, settings.ExpansionMode);
        Assert.True(settings.AutoHide);
        Assert.Equal(GlancePlacement.Bottom, settings.Placement);
        Assert.False(settings.ShowSetupOnStartup);
        Assert.True(preferences.IsEnabled("Weather"));
        Assert.False(preferences.IsEnabled("Timer"));
        Assert.True(writer.WriteCount >= 2);
    }

    private static SetupTourViewModel CreateViewModel(GlanceSettings settings, ModulePreferenceService preferences, TestWritableOptions writer, TestGlanceModuleFeedService? feed = null, ModuleInstallationService? installations = null) => new(settings,
            preferences,
            writer,
            new GlanceModuleCategoryResolver(new TestLocalizer()),
            feed ?? new TestGlanceModuleFeedService(),
            new TestGlanceModulePackageService(),
            installations ?? new ModuleInstallationService(),
            new ImmediateTestDispatcher(),
            new TestLocalizer(),
            NullLogger<SetupTourViewModel>.Instance);

    private sealed class TestComponent(string id) :
        IGlanceComponent
    {
        public string Id { get; } = id;

        public string DisplayName => Id;

        public string Description => $"{Id} description";

        public int Order => 0;

        public object CompactContent { get; } = new();

        public object ExpandedContent { get; } = new();
    }

    private sealed class TestLocalizer :
        ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) => key;
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
