using Glance.Application.Abstractions;

namespace Glance.AudioSwitcher.Tests;

public sealed class AudioSwitcherViewModelTests
{
    [Fact]
    public void Constructor_UsesCurrentDefaultOutput()
    {
        FakeAudioDeviceService service = new(new AudioOutputDevice("speakers", "Speakers", false), new AudioOutputDevice("headphones", "Headphones", true));

        AudioSwitcherViewModel viewModel = new(service, new FakeLocalizer());

        Assert.Equal("Headphones", viewModel.CurrentDeviceName);
        Assert.Equal("headphones", viewModel.SelectedDevice?.Id);
        Assert.Equal(2, viewModel.Devices.Count);
        Assert.True(viewModel.HasDevices);
    }

    [Fact]
    public void Constructor_HandlesMissingOutputs()
    {
        AudioSwitcherViewModel viewModel = new(new FakeAudioDeviceService(), new FakeLocalizer());

        Assert.Equal("No audio output", viewModel.CurrentDeviceName);
        Assert.Null(viewModel.SelectedDevice);
        Assert.Empty(viewModel.Devices);
        Assert.False(viewModel.HasDevices);
    }

    [Fact]
    public void SelectingDevice_SwitchesDefaultOutput()
    {
        FakeAudioDeviceService service = new(new AudioOutputDevice("speakers", "Speakers", false), new AudioOutputDevice("headphones", "Headphones", true));
        AudioSwitcherViewModel viewModel = new(service, new FakeLocalizer());

        viewModel.SelectedDevice = viewModel.Devices[0];

        Assert.Equal("speakers", service.LastSelectedId);
        Assert.Equal("Speakers", viewModel.CurrentDeviceName);
    }

    [Fact]
    public void SelectingCurrentDevice_DoesNotSetDefaultAgain()
    {
        FakeAudioDeviceService service = new(new AudioOutputDevice("speakers", "Speakers", true), new AudioOutputDevice("headphones", "Headphones", false));
        AudioSwitcherViewModel viewModel = new(service, new FakeLocalizer());

        viewModel.SelectedDevice = viewModel.Devices[0];

        Assert.Null(service.LastSelectedId);
    }

    [Fact]
    public void FailedSwitch_KeepsCurrentOutput()
    {
        FakeAudioDeviceService service = new(new AudioOutputDevice("speakers", "Speakers", true), new AudioOutputDevice("headphones", "Headphones", false))
        {
            CanSetDefault = false
        };
        AudioSwitcherViewModel viewModel = new(service, new FakeLocalizer());

        viewModel.SelectedDevice = viewModel.Devices[1];

        Assert.Equal("headphones", service.LastSelectedId);
        Assert.Equal("Speakers", viewModel.CurrentDeviceName);
        Assert.Equal("speakers", viewModel.SelectedDevice?.Id);
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
