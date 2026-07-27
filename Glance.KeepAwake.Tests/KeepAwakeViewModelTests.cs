using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;

namespace Glance.KeepAwake.Tests;

public sealed class KeepAwakeViewModelTests
{
    [Fact]
    public void Constructor_UsesServiceState()
    {
        KeepAwakeViewModel viewModel = new(new FakeKeepAwakeService(true), new FakeLocalizer());

        Assert.True(viewModel.IsActive);
        Assert.Equal("Awake", viewModel.StatusText);
        Assert.Equal("Stop", viewModel.ActionLabel);
    }

    [Fact]
    public async Task ToggleAsync_StartsKeepAwake()
    {
        FakeKeepAwakeService service = new(false);
        KeepAwakeViewModel viewModel = new(service, new FakeLocalizer());

        await viewModel.ToggleAsync();

        Assert.True(service.IsActive);
        Assert.True(viewModel.IsActive);
        Assert.Equal("Awake", viewModel.StatusText);
        Assert.Equal("\uE71A", viewModel.ActionGlyph);
    }

    [Fact]
    public async Task ToggleAsync_StopsKeepAwake()
    {
        FakeKeepAwakeService service = new(true);
        KeepAwakeViewModel viewModel = new(service, new FakeLocalizer());

        await viewModel.ToggleAsync();

        Assert.False(service.IsActive);
        Assert.False(viewModel.IsActive);
        Assert.Equal("Ready", viewModel.StatusText);
        Assert.Equal("\uE768", viewModel.ActionGlyph);
    }

    [Fact]
    public async Task ToggleAsync_PreservesStateWhenRequestFails()
    {
        FakeKeepAwakeService service = new(false)
        {
            ShouldSucceed = false
        };
        KeepAwakeViewModel viewModel = new(service, new FakeLocalizer());

        await viewModel.ToggleAsync();

        Assert.False(viewModel.IsActive);
        Assert.Equal("Ready", viewModel.StatusText);
    }

    [Fact]
    public async Task ToggleAsync_DispatchesCompletedState()
    {
        FakeDispatcher dispatcher = new();
        KeepAwakeViewModel viewModel = new(new FakeKeepAwakeService(false), new FakeLocalizer(), dispatcher);

        await viewModel.ToggleAsync();

        Assert.Single(dispatcher.Actions);
        Assert.False(viewModel.IsActive);
        Assert.True(viewModel.IsBusy);

        dispatcher.Actions[0]();

        Assert.True(viewModel.IsActive);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task RestoreAsync_DoesNotStartWhenDisabled()
    {
        FakeKeepAwakeService service = new(false);
        KeepAwakeViewModel viewModel = new(service, new FakeLocalizer());

        await viewModel.RestoreAsync(false);

        Assert.False(service.IsActive);
        Assert.False(viewModel.IsActive);
    }

    [Fact]
    public async Task RestoreAsync_StartsPreviousSessionWhenEnabled()
    {
        FakeKeepAwakeService service = new(false);
        KeepAwakeViewModel viewModel = new(service, new FakeLocalizer());

        await viewModel.RestoreAsync(true);

        Assert.True(service.IsActive);
        Assert.True(viewModel.IsActive);
    }

    private sealed class FakeKeepAwakeService(bool isActive) :
        IKeepAwakeService
    {
        public bool IsActive { get; private set; } = isActive;

        public bool ShouldSucceed { get; init; } = true;

        public Task<bool> SetActiveAsync(bool isActive,
            CancellationToken cancellationToken = default)
        {
            if (ShouldSucceed)
            {
                IsActive = isActive;
            }

            return Task.FromResult(ShouldSucceed);
        }
    }

    private sealed class FakeLocalizer :
        ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) => key switch
        {
            "ActiveStatus" => "Awake",
            "InactiveStatus" => "Ready",
            "ActiveDetail" => "Automatic sleep is prevented",
            "InactiveDetail" => "Windows can sleep normally",
            "StartLabel" => "Start",
            "StopLabel" => "Stop",
            _ => key
        };
    }

    private sealed class FakeDispatcher :
        IDispatcher
    {
        public List<Action> Actions { get; } = [];

        public void Dispatch(Action action) => Actions.Add(action);
    }
}
