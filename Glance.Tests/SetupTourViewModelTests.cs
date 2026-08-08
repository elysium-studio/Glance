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
        Assert.False(viewModel.CanGoBack);
        Assert.True(viewModel.CanGoNext);

        viewModel.GoNext();
        viewModel.GoNext();
        viewModel.GoNext();

        Assert.True(viewModel.IsLastPage);
        Assert.False(viewModel.CanGoNext);

        viewModel.GoBack();

        Assert.Equal(2, viewModel.CurrentPage);
        Assert.True(viewModel.CanGoBack);
    }

    [Fact]
    public async Task ChoicesApplyImmediatelyAndFinishDisablesTheTour()
    {
        GlanceSettings settings = new();
        TestWritableOptions writer = new(settings);
        ModulePreferenceService preferences = new([new TestComponent("Weather"), new TestComponent("Timer")], settings, writer);
        SetupTourViewModel viewModel = CreateViewModel(settings, preferences, writer);
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.Finished += (_, _) => completion.SetResult();
        await viewModel.SelectExpansionModeAsync(GlanceExpansionMode.AlwaysExpanded);
        await viewModel.SelectAutoHideAsync(true);
        viewModel.Modules[1].IsEnabled = false;

        Assert.Equal(GlanceExpansionMode.AlwaysExpanded, settings.ExpansionMode);
        Assert.True(settings.AutoHide);
        Assert.True(settings.ShowSetupOnStartup);

        viewModel.Finish();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(GlanceExpansionMode.AlwaysExpanded, settings.ExpansionMode);
        Assert.True(settings.AutoHide);
        Assert.False(settings.ShowSetupOnStartup);
        Assert.True(preferences.IsEnabled("Weather"));
        Assert.False(preferences.IsEnabled("Timer"));
        Assert.True(writer.WriteCount >= 2);
    }

    private static SetupTourViewModel CreateViewModel(GlanceSettings settings,
        ModulePreferenceService preferences,
        TestWritableOptions writer) => new(settings,
            preferences,
            writer,
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
