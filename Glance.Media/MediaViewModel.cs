using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.Media;

public partial class MediaViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = "Nothing playing";

    [ObservableProperty]
    private string artist = "Open a media app to begin";

    [ObservableProperty]
    private string source = "Media";

    [ObservableProperty]
    private object? artwork;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseGlyph))]
    private bool isPlaying;

    [ObservableProperty]
    private bool hasSession;

    public string PlayPauseGlyph => IsPlaying ? "\uF8AE" : "\uF5B0";

    public event EventHandler<MediaPlaybackAction>? PlaybackActionRequested;

    public void Previous() =>
        PlaybackActionRequested?.Invoke(this, MediaPlaybackAction.Previous);

    public void TogglePlayback() =>
        PlaybackActionRequested?.Invoke(this, MediaPlaybackAction.TogglePlayback);

    public void Next() =>
        PlaybackActionRequested?.Invoke(this, MediaPlaybackAction.Next);
}
