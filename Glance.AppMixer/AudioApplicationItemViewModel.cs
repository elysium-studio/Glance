using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.AppMixer;

public sealed partial class AudioApplicationItemViewModel :
    ObservableObject
{
    private readonly IAudioApplicationService service;
    private bool isUpdating;

    [ObservableProperty]
    private string displayName;

    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    private bool isForeground;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MuteGlyph))]
    [NotifyPropertyChangedFor(nameof(MuteLabel))]
    private bool isMuted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PeakScale))]
    private double peakPercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PeakScale))]
    [NotifyPropertyChangedFor(nameof(VolumeText))]
    private double volume;

    public AudioApplicationItemViewModel(AudioApplicationSession session,
        IAudioApplicationService service)
    {
        this.service = service;
        Id = session.Id;
        displayName = session.DisplayName;
        Update(session);
    }

    public string Id { get; }

    public string VolumeText => $"{Math.Round(Volume):0}%";

    public string MuteGlyph => IsMuted ? "\uE74F" : "\uE767";

    public string MuteLabel => IsMuted ? "Unmute" : "Mute";

    public double PeakScale => Math.Min(PeakPercent, Volume) / 100;

    public void ToggleMute() => IsMuted = !IsMuted;

    public void Update(AudioApplicationSession session)
    {
        isUpdating = true;
        DisplayName = session.DisplayName;
        IsActive = session.IsActive;
        IsForeground = session.IsForeground;
        IsMuted = session.IsMuted;
        PeakPercent = Math.Clamp(session.Peak * 100, 0, 100);
        Volume = session.VolumePercent;
        isUpdating = false;
    }

    partial void OnIsMutedChanged(bool value)
    {
        if (!isUpdating && !service.TrySetMuted(Id, value))
        {
            isUpdating = true;
            IsMuted = !value;
            isUpdating = false;
        }
    }

    partial void OnVolumeChanged(double value)
    {
        if (isUpdating)
        {
            return;
        }

        int volumePercent = (int)Math.Round(Math.Clamp(value, 0, 100));

        if (!service.TrySetVolume(Id, volumePercent))
        {
            return;
        }

        OnPropertyChanged(nameof(VolumeText));
    }
}
