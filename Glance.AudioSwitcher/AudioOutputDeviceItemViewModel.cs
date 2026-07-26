using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.AudioSwitcher;

public sealed partial class AudioOutputDeviceItemViewModel(AudioOutputDevice device,
    IAudioDeviceService audioDeviceService) :
    ObservableObject
{
    private readonly IAudioDeviceService audioDeviceService = audioDeviceService;

    [ObservableProperty]
    private string name = device.Name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumeText))]
    private int volumePercent = device.VolumePercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleGlyph))]
    private bool isMuted = device.IsMuted;

    [ObservableProperty]
    private bool isDefault = device.IsDefault;

    public AudioOutputDevice Device { get; private set; } = device;

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
