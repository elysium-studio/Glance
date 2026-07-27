using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.Shell;
using Xunit;

namespace Glance.Tests;

public sealed class ModulesViewModelTests
{
    [Fact]
    public void CancellingReorderingRestoresOriginalOrder()
    {
        TestContext context = new();
        ModulesViewModel viewModel = context.CreateViewModel();

        viewModel.BeginReordering();
        viewModel.Move(0, 2);
        viewModel.CancelReordering();

        Assert.Equal(["Media", "Timer", "Power"], viewModel.Select(item => ((ModuleSettingsItemViewModel)item).Id));
        Assert.All(viewModel.Cast<ModuleSettingsItemViewModel>(), item => Assert.False(item.IsReordering));
        Assert.Equal(0, context.Writer.WriteCount);
    }

    [Fact]
    public async Task CompletingReorderingPersistsCurrentOrder()
    {
        TestContext context = new();
        ModulesViewModel viewModel = context.CreateViewModel();

        viewModel.BeginReordering();
        viewModel.Move(0, 2);
        await viewModel.CompleteReorderingAsync();

        Assert.Equal(["Timer", "Power", "Media"], context.Settings.Modules.Select(item => item.Id));
        Assert.False(viewModel.IsReordering);
        Assert.Equal(1, context.Writer.WriteCount);
    }

    private sealed class TestContext
    {
        private readonly IGlanceComponent[] components =
        [
            new TestComponent("Media", 0),
            new TestComponent("Timer", 1),
            new TestComponent("Power", 2)
        ];

        public TestContext()
        {
            Settings.Modules =
            [
                new GlanceModulePreference { Id = "Media" },
                new GlanceModulePreference { Id = "Timer" },
                new GlanceModulePreference { Id = "Power" }
            ];
            Writer = new TestWritableOptions(Settings);
        }

        public GlanceSettings Settings { get; } = new();

        public TestWritableOptions Writer { get; }

        public ModulesViewModel CreateViewModel()
        {
            ModulePreferenceService preferences = new(components, Settings, Writer);
            return new ModulesViewModel(null!,
                null!,
                WeakReferenceMessenger.Default,
                null!,
                preferences,
                new TestTextLocalizer(),
                []);
        }
    }

    private sealed class TestComponent(string id, int order) :
        IGlanceComponent
    {
        public string Id { get; } = id;

        public string DisplayName => Id;

        public string Description => string.Empty;

        public int Order { get; } = order;

        public object CompactContent { get; } = new();

        public object ExpandedContent { get; } = new();
    }

    private sealed class TestTextLocalizer :
        ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) => key;
    }

    private sealed class TestWritableOptions(GlanceSettings settings) :
        IWritableOptions<GlanceSettings>
    {
        public int WriteCount { get; private set; }

        public Task<GlanceSettings?> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<GlanceSettings?>(settings);

        public Task WriteAsync(Action<GlanceSettings> update,
            CancellationToken cancellationToken = default)
        {
            update(settings);
            WriteCount++;
            return Task.CompletedTask;
        }

        public Task WriteAsync(GlanceSettings value,
            CancellationToken cancellationToken = default)
        {
            WriteCount++;
            return Task.CompletedTask;
        }
    }
}
