using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.AudioSwitcher;

public sealed partial class AudioOutputDeviceItemViewModel :
    ObservableObject
{
    private readonly IAudioDeviceService audioDeviceService;

    [ObservableProperty]
    private string name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumeText))]
    private int volumePercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleGlyph))]
    private bool isMuted;

    [ObservableProperty]
    private bool isDefault;

    public AudioOutputDeviceItemViewModel(
        AudioOutputDevice device,
        IAudioDeviceService audioDeviceService)
    {
        Device = device;
        this.audioDeviceService = audioDeviceService;
        name = device.Name;
        volumePercent = device.VolumePercent;
        isMuted = device.IsMuted;
        isDefault = device.IsDefault;
    }

    public AudioOutputDevice Device { get; private set; }

    public string Id => Device.Id;

    public string VolumeText => $"{VolumePercent}%";

    public string ToggleGlyph => IsMuted ? "\uE74F" : "\uE767";

    public void ToggleMute()
    {
        bool target = !IsMuted;

        if (audioDeviceService.TrySetOutputMuted(Id, target))
        {
            IsMuted = target;
        }
    }

    public void Update(AudioOutputDevice device)
    {
        Device = device;
        Name = device.Name;
        VolumePercent = device.VolumePercent;
        IsMuted = device.IsMuted;
        IsDefault = device.IsDefault;
    }
}
