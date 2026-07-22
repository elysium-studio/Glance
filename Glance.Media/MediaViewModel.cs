using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;

namespace Glance.Media;

public sealed partial class MediaViewModel :
    ObservableObject,
    IRecipient<OptionsChangedEventArgs<MediaSettings>>,
    IDisposable
{
    private readonly IDispatcher? dispatcher;
    private readonly IMessenger? messenger;

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

    [ObservableProperty]
    private bool showAudioVisualization;

    public MediaViewModel(ITextLocalizer localizer, MediaSettings? settings = null, IMessenger? messenger = null, IDispatcher? dispatcher = null)
    {
        MediaSettings initialSettings = settings ?? new MediaSettings();

        this.dispatcher = dispatcher;
        this.messenger = messenger;
        title = localizer.GetText("NothingPlaying");
        artist = localizer.GetText("OpenMediaApp");
        source = localizer.GetText("ModuleTitle");
        showAudioVisualization = initialSettings.ShowAudioVisualization;
        messenger?.Register<OptionsChangedEventArgs<MediaSettings>>(this);
    }

    public string PlayPauseGlyph => IsPlaying ? "\uF8AE" : "\uF5B0";

    public event EventHandler<AudioLevelsChangedEventArgs>? AudioLevelsChanged;

    public event EventHandler<MediaPlaybackAction>? PlaybackActionRequested;

    public void Receive(OptionsChangedEventArgs<MediaSettings> message)
    {
        void Apply() => ShowAudioVisualization = message.Options.ShowAudioVisualization;

        if (dispatcher is null)
        {
            Apply();
        }
        else
        {
            dispatcher.Dispatch(Apply);
        }
    }

    public void Dispose()
    {
        messenger?.UnregisterAll(this);
        GC.SuppressFinalize(this);
    }

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
