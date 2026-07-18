using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Glance.Media;

public partial class MediaViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = "Nothing playing";

    [ObservableProperty]
    private string artist = "Open a media app to begin";

    [ObservableProperty]
    private string source = "MEDIA";

    [ObservableProperty]
    private object? artwork;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseGlyph))]
    private bool isPlaying;

    [ObservableProperty]
    private bool hasSession;

    public string PlayPauseGlyph => IsPlaying ? "\uF8AE" : "\uF5B0";

    public event EventHandler<MediaPlaybackAction>? PlaybackActionRequested;

    [RelayCommand]
    private void Previous() =>
        PlaybackActionRequested?.Invoke(this, MediaPlaybackAction.Previous);

    [RelayCommand]
    private void TogglePlayback() =>
        PlaybackActionRequested?.Invoke(this, MediaPlaybackAction.TogglePlayback);

    [RelayCommand]
    private void Next() =>
        PlaybackActionRequested?.Invoke(this, MediaPlaybackAction.Next);
}
