using Glance.Application.Abstractions;

namespace Glance.PrivacyControls.Tests;

public sealed class PrivacyControlsViewModelTests
{
    [Fact]
    public void Constructor_ShowsAvailableMicrophone()
    {
        FakeMicrophoneService service = new(new MicrophoneState("Studio microphone", true, false, 0));

        PrivacyControlsViewModel viewModel = new(service, new FakeLocalizer());

        Assert.True(viewModel.IsAvailable);
        Assert.False(viewModel.IsMuted);
        Assert.Equal("Studio microphone", viewModel.DeviceName);
        Assert.Equal("Ready", viewModel.StatusText);
    }

    [Fact]
    public void Constructor_HandlesMissingMicrophone()
    {
        PrivacyControlsViewModel viewModel = new(new FakeMicrophoneService(MicrophoneState.Unavailable), new FakeLocalizer());

        Assert.False(viewModel.IsAvailable);
        Assert.Equal("No microphone", viewModel.DeviceName);
        Assert.Equal("No microphone", viewModel.StatusText);
    }

    [Fact]
    public void ToggleMute_ChangesEndpointMuteState()
    {
        FakeMicrophoneService service = new(new MicrophoneState("Microphone", true, false, 0));
        PrivacyControlsViewModel viewModel = new(service, new FakeLocalizer());

        viewModel.ToggleMute();

        Assert.True(service.State.IsMuted);
        Assert.True(viewModel.IsMuted);
        Assert.Equal("Muted", viewModel.StatusText);
        Assert.Equal("\uE74F", viewModel.ToggleGlyph);
    }

    [Fact]
    public void Update_ReportsInputActivityAndNormalizedLevel()
    {
        PrivacyControlsViewModel viewModel = new(new FakeMicrophoneService(MicrophoneState.Unavailable), new FakeLocalizer());
        double level = 0;
        viewModel.LevelChanged += (_, args) => level = args.Level;

        viewModel.Update(new MicrophoneState("Microphone", true, false, 0.25));

        Assert.True(viewModel.IsActive);
        Assert.Equal("Active", viewModel.StatusText);
        Assert.InRange(level, 0.60, 0.63);
    }

    [Fact]
    public void MutedMicrophone_ReportsNoInputLevel()
    {
        PrivacyControlsViewModel viewModel = new(new FakeMicrophoneService(MicrophoneState.Unavailable), new FakeLocalizer());
        double level = 1;
        viewModel.LevelChanged += (_, args) => level = args.Level;

        viewModel.Update(new MicrophoneState("Microphone", true, true, 0.8));

        Assert.False(viewModel.IsActive);
        Assert.Equal(0, level);
    }

    private sealed class FakeMicrophoneService(MicrophoneState state) :
        IMicrophoneService
    {
        public MicrophoneState State { get; private set; } = state;

        public MicrophoneState GetState() => State;

        public bool TrySetMuted(bool isMuted)
        {
            State = State with
            {
                IsMuted = isMuted
            };
            return true;
        }
    }

    private sealed class FakeLocalizer :
        ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) => key switch
        {
            "NoMicrophone" => "No microphone",
            "MicrophoneMuted" => "Muted",
            "MicrophoneActive" => "Active",
            "MicrophoneReady" => "Ready",
            _ => key
        };
    }
}
