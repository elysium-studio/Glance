using Glance.Application.Abstractions;

namespace Glance.PrivacyControls.Tests;

public sealed class PrivacyControlsViewModelTests
{
    [Fact]
    public void Constructor_ShowsAvailableMicrophone()
    {
        FakeMicrophoneService service = new(new MicrophoneState("Studio microphone", true, false, false));

        PrivacyControlsViewModel viewModel = CreateViewModel(service);

        Assert.True(viewModel.IsAvailable);
        Assert.False(viewModel.IsMuted);
        Assert.True(viewModel.IsMicrophoneLive);
        Assert.Equal("Studio microphone", viewModel.DeviceName);
        Assert.Equal("Ready", viewModel.StatusText);
        Assert.Equal("\uF8AE", viewModel.ToggleGlyph);
    }

    [Fact]
    public void Constructor_HandlesMissingMicrophone()
    {
        PrivacyControlsViewModel viewModel = CreateViewModel(new FakeMicrophoneService(MicrophoneState.Unavailable));

        Assert.False(viewModel.IsAvailable);
        Assert.Equal("No microphone", viewModel.DeviceName);
        Assert.Equal("No microphone", viewModel.StatusText);
    }

    [Fact]
    public void ToggleMute_ChangesEndpointMuteState()
    {
        FakeMicrophoneService service = new(new MicrophoneState("Microphone", true, false, false));
        PrivacyControlsViewModel viewModel = CreateViewModel(service);

        viewModel.ToggleMute();

        Assert.True(service.State.IsMuted);
        Assert.True(viewModel.IsMuted);
        Assert.False(viewModel.IsMicrophoneLive);
        Assert.Equal("Muted", viewModel.StatusText);
        Assert.Equal("\uF5B0", viewModel.ToggleGlyph);
    }

    [Fact]
    public void Update_ReportsMicrophoneUsage()
    {
        PrivacyControlsViewModel viewModel = CreateViewModel(new FakeMicrophoneService(MicrophoneState.Unavailable));

        viewModel.Update(new MicrophoneState("Microphone", true, false, true), false);

        Assert.True(viewModel.IsActive);
        Assert.Equal("Active", viewModel.StatusText);
    }

    [Fact]
    public void MutedMicrophone_StillReportsApplicationUsage()
    {
        PrivacyControlsViewModel viewModel = CreateViewModel(new FakeMicrophoneService(MicrophoneState.Unavailable));

        viewModel.Update(new MicrophoneState("Microphone", true, true, true), false);

        Assert.True(viewModel.IsActive);
        Assert.Equal("Active", viewModel.StatusText);
    }

    [Fact]
    public void Update_ReportsCameraUsage()
    {
        PrivacyControlsViewModel viewModel = CreateViewModel(new FakeMicrophoneService(MicrophoneState.Unavailable));

        viewModel.Update(MicrophoneState.Unavailable, true);

        Assert.True(viewModel.IsCameraActive);
        Assert.Equal("Camera active", viewModel.StatusText);
        Assert.Equal("\uE722", viewModel.StatusGlyph);
    }

    [Fact]
    public void Update_ReportsCombinedUsage()
    {
        PrivacyControlsViewModel viewModel = CreateViewModel(new FakeMicrophoneService(MicrophoneState.Unavailable));

        viewModel.Update(new MicrophoneState("Microphone", true, false, true), true);

        Assert.True(viewModel.IsActive);
        Assert.True(viewModel.IsCameraActive);
        Assert.Equal("Microphone and camera active", viewModel.StatusText);
    }

    private static PrivacyControlsViewModel CreateViewModel(FakeMicrophoneService microphoneService,
        bool isCameraInUse = false) =>
        new(microphoneService, new FakeCameraUsageService(isCameraInUse), new FakeLocalizer());

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

    private sealed class FakeCameraUsageService(bool isInUse) :
        ICameraUsageService
    {
        public bool IsInUse() => isInUse;
    }

    private sealed class FakeLocalizer :
        ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) => key switch
        {
            "NoMicrophone" => "No microphone",
            "MicrophoneMuted" => "Muted",
            "MicrophoneActive" => "Active",
            "CameraActive" => "Camera active",
            "MicrophoneAndCameraActive" => "Microphone and camera active",
            "MicrophoneReady" => "Ready",
            _ => key
        };
    }
}
