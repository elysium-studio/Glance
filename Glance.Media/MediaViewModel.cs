using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;

namespace Glance.Media;

public partial class MediaViewModel : ObservableObject
{
    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private string artist;

    [ObservableProperty]
    private string source;

    [ObservableProperty]
    private object? artwork;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseGlyph))]
    private bool isPlaying;

    [ObservableProperty]
    private bool hasSession;

    [ObservableProperty]
    private bool canSkipPrevious;

    [ObservableProperty]
    private bool canSkipNext;

    [ObservableProperty]
    private bool canTogglePlayback;

    public MediaViewModel(ITextLocalizer localizer)
    {
        title = localizer.GetText("NothingPlaying");
        artist = localizer.GetText("OpenMediaApp");
        source = localizer.GetText("ModuleTitle");
    }

    public string PlayPauseGlyph => IsPlaying ? "\uF8AE" : "\uF5B0";

    public event EventHandler<AudioLevelsChangedEventArgs>? AudioLevelsChanged;

    public event EventHandler<MediaPlaybackAction>? PlaybackActionRequested;

    public void UpdateAudioLevels(IReadOnlyList<double> levels) =>
        AudioLevelsChanged?.Invoke(this, new AudioLevelsChangedEventArgs([.. levels]));

    public void Previous() =>
        PlaybackActionRequested?.Invoke(this, MediaPlaybackAction.Previous);

    public void TogglePlayback() =>
        PlaybackActionRequested?.Invoke(this, MediaPlaybackAction.TogglePlayback);

    public void Next() =>
        PlaybackActionRequested?.Invoke(this, MediaPlaybackAction.Next);
}

public sealed class AudioLevelsChangedEventArgs(IReadOnlyList<double> levels) : EventArgs
{
    public IReadOnlyList<double> Levels { get; } = levels;
}
