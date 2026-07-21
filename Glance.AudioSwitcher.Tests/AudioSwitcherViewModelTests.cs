using Glance.Application.Abstractions;

namespace Glance.AudioSwitcher.Tests;

public sealed class AudioSwitcherViewModelTests
{
    [Fact]
    public void Constructor_UsesCurrentDefaultOutput()
    {
        FakeAudioDeviceService service = new(
            new AudioOutputDevice("speakers", "Speakers", false),
            new AudioOutputDevice("headphones", "Headphones", true));

        AudioSwitcherViewModel viewModel = new(service, new FakeLocalizer());

        Assert.Equal("Headphones", viewModel.CurrentDeviceName);
        Assert.Equal("2 / 2", viewModel.DevicePositionText);
        Assert.True(viewModel.CanSwitch);
    }

    [Fact]
    public void Constructor_HandlesMissingOutputs()
    {
        AudioSwitcherViewModel viewModel = new(
            new FakeAudioDeviceService(),
            new FakeLocalizer());

        Assert.Equal("No audio output", viewModel.CurrentDeviceName);
        Assert.Equal(string.Empty, viewModel.DevicePositionText);
        Assert.False(viewModel.CanSwitch);
    }

    [Fact]
    public void Next_SwitchesToFollowingOutputAndWraps()
    {
        FakeAudioDeviceService service = new(
            new AudioOutputDevice("speakers", "Speakers", false),
            new AudioOutputDevice("headphones", "Headphones", true));
        AudioSwitcherViewModel viewModel = new(service, new FakeLocalizer());

        viewModel.Next();

        Assert.Equal("speakers", service.LastSelectedId);
        Assert.Equal("Speakers", viewModel.CurrentDeviceName);
        Assert.Equal("1 / 2", viewModel.DevicePositionText);
    }

    [Fact]
    public void Previous_SwitchesToPreviousOutputAndWraps()
    {
        FakeAudioDeviceService service = new(
            new AudioOutputDevice("speakers", "Speakers", true),
            new AudioOutputDevice("headphones", "Headphones", false));
        AudioSwitcherViewModel viewModel = new(service, new FakeLocalizer());

        viewModel.Previous();

        Assert.Equal("headphones", service.LastSelectedId);
        Assert.Equal("Headphones", viewModel.CurrentDeviceName);
        Assert.Equal("2 / 2", viewModel.DevicePositionText);
    }

    [Fact]
    public void Switch_DoesNothingWithOnlyOneOutput()
    {
        FakeAudioDeviceService service = new(
            new AudioOutputDevice("speakers", "Speakers", true));
        AudioSwitcherViewModel viewModel = new(service, new FakeLocalizer());

        viewModel.Next();

        Assert.Null(service.LastSelectedId);
        Assert.False(viewModel.CanSwitch);
    }

    [Fact]
    public void FailedSwitch_KeepsCurrentOutput()
    {
        FakeAudioDeviceService service = new(
            new AudioOutputDevice("speakers", "Speakers", true),
            new AudioOutputDevice("headphones", "Headphones", false))
        {
            CanSetDefault = false
        };
        AudioSwitcherViewModel viewModel = new(service, new FakeLocalizer());

        viewModel.Next();

        Assert.Equal("headphones", service.LastSelectedId);
        Assert.Equal("Speakers", viewModel.CurrentDeviceName);
        Assert.Equal("1 / 2", viewModel.DevicePositionText);
    }

    private sealed class FakeAudioDeviceService(params AudioOutputDevice[] devices) :
        IAudioDeviceService
    {
        private List<AudioOutputDevice> devices = [.. devices];

        public event EventHandler? DevicesChanged;

        public bool CanSetDefault { get; init; } = true;

        public string? LastSelectedId { get; private set; }

        public IReadOnlyList<AudioOutputDevice> GetOutputDevices() => devices;

        public bool TrySetDefaultOutput(string deviceId)
        {
            LastSelectedId = deviceId;

            if (!CanSetDefault)
            {
                return false;
            }

            devices = [.. devices.Select(device => device with
            {
                IsDefault = device.Id == deviceId
            })];
            DevicesChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }

    private sealed class FakeLocalizer : ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) => key switch
        {
            "NoOutputDevice" => "No audio output",
            _ => key
        };
    }
}
