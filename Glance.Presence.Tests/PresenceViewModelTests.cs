using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;

namespace Glance.Presence.Tests;

public sealed class PresenceViewModelTests
{
    [Fact]
    public void Constructor_UsesServiceState()
    {
        PresenceViewModel viewModel = new(new FakePresenceService(true), new FakeLocalizer());

        Assert.True(viewModel.IsActive);
        Assert.Equal("Presence active", viewModel.StatusText);
        Assert.Equal("Stop", viewModel.ActionLabel);
    }

    [Fact]
    public async Task ToggleAsync_StartsPresence()
    {
        FakePresenceService service = new(false);
        PresenceViewModel viewModel = new(service, new FakeLocalizer());

        await viewModel.ToggleAsync();

        Assert.True(service.IsActive);
        Assert.True(viewModel.IsActive);
        Assert.Equal("Presence active", viewModel.StatusText);
        Assert.Equal("\uE71A", viewModel.ActionGlyph);
    }

    [Fact]
    public async Task ToggleAsync_StopsPresence()
    {
        FakePresenceService service = new(true);
        PresenceViewModel viewModel = new(service, new FakeLocalizer());

        await viewModel.ToggleAsync();

        Assert.False(service.IsActive);
        Assert.False(viewModel.IsActive);
        Assert.Equal("Ready", viewModel.StatusText);
        Assert.Equal("\uE768", viewModel.ActionGlyph);
    }

    [Fact]
    public async Task ToggleAsync_PreservesStateWhenRequestFails()
    {
        FakePresenceService service = new(false)
        {
            ShouldSucceed = false
        };
        PresenceViewModel viewModel = new(service, new FakeLocalizer());

        await viewModel.ToggleAsync();

        Assert.False(viewModel.IsActive);
        Assert.Equal("Ready", viewModel.StatusText);
    }

    [Fact]
    public async Task ToggleAsync_DispatchesCompletedState()
    {
        FakeDispatcher dispatcher = new();
        PresenceViewModel viewModel = new(new FakePresenceService(false), new FakeLocalizer(), dispatcher);

        await viewModel.ToggleAsync();

        _ = Assert.Single(dispatcher.Actions);
        Assert.False(viewModel.IsActive);
        Assert.True(viewModel.IsBusy);

        dispatcher.Actions[0]();

        Assert.True(viewModel.IsActive);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task RestoreAsync_DoesNotStartWhenDisabled()
    {
        FakePresenceService service = new(false);
        PresenceViewModel viewModel = new(service, new FakeLocalizer());

        await viewModel.RestoreAsync(false);

        Assert.False(service.IsActive);
        Assert.False(viewModel.IsActive);
    }

    [Fact]
    public async Task RestoreAsync_StartsPreviousSessionWhenEnabled()
    {
        FakePresenceService service = new(false);
        PresenceViewModel viewModel = new(service, new FakeLocalizer());

        await viewModel.RestoreAsync(true);

        Assert.True(service.IsActive);
        Assert.True(viewModel.IsActive);
    }

    private sealed class FakePresenceService(bool isActive) :
        IPresenceService
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
            "ActiveStatus" => "Presence active",
            "InactiveStatus" => "Ready",
            "ActiveDetail" => "Input is sent only while idle",
            "InactiveDetail" => "No input is being simulated",
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
