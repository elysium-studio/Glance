using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;

namespace Glance.Spotify;

public sealed partial class SpotifyViewModel :
    ObservableObject,
    IRecipient<OptionsChangedEventArgs<SpotifySettings>>,
    IDisposable
{
    private readonly IDispatcher dispatcher;
    private readonly ITextLocalizer localizer;
    private readonly IMessenger messenger;
    private bool applyingSnapshot;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfigured))]
    private string clientId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    [NotifyPropertyChangedFor(nameof(IsNotConnected))]
    private SpotifyConnectionState connectionState;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackText))]
    private string title;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackText))]
    private string artist;

    [ObservableProperty]
    private string album = string.Empty;

    [ObservableProperty]
    private string? artworkUrl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackSourceName))]
    private string deviceName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackText))]
    private bool hasPlayback;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseGlyph))]
    private bool isPlaying;

    [ObservableProperty]
    private bool canControlPlayback;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressScale))]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private double progressMilliseconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressScale))]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private double durationMilliseconds = 1;

    [ObservableProperty]
    private int volumePercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDevices))]
    private IReadOnlyList<SpotifyDevice> devices = [];

    [ObservableProperty]
    private string? selectedDeviceId;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public SpotifyViewModel(ITextLocalizer localizer,
        SpotifySettings settings,
        IMessenger messenger,
        IDispatcher dispatcher)
    {
        this.localizer = localizer;
        this.messenger = messenger;
        this.dispatcher = dispatcher;
        clientId = settings.ClientId;
        title = localizer.GetText("NotConnected");
        artist = localizer.GetText("ConnectInSettings");
        messenger.Register(this);
    }

    public bool IsConfigured => SpotifyClientIdValidator.IsValid(ClientId);

    public bool IsConnected => ConnectionState == SpotifyConnectionState.Connected;

    public bool IsNotConnected => !IsConnected;

    public string PlayPauseGlyph => IsPlaying ? "\uF8AE" : "\uF5B0";

    public string PlaybackSourceName => string.IsNullOrWhiteSpace(DeviceName) ? "Spotify" : DeviceName;

    public string PlaybackText => HasPlayback && !string.IsNullOrWhiteSpace(Artist)
        ? $"{Title} · {Artist}"
        : Title;

    public bool HasDevices => Devices.Count > 0;

    public double ProgressScale => Math.Clamp(ProgressMilliseconds / Math.Max(1, DurationMilliseconds), 0, 1);

    public string ProgressText => $"{FormatTime(ProgressMilliseconds)} / {FormatTime(DurationMilliseconds)}";

    public event EventHandler<SpotifyPlaybackAction>? PlaybackActionRequested;

    public void Receive(OptionsChangedEventArgs<SpotifySettings> message) =>
        dispatcher.Dispatch(() => ClientId = message.Options.ClientId);

    public void ApplyConnectionState(SpotifyConnectionState state, string? errorMessage = null)
    {
        ConnectionState = state;
        StatusMessage = errorMessage ?? string.Empty;

        if (state == SpotifyConnectionState.Connected)
        {
            if (!HasPlayback)
            {
                ShowIdle();
            }
            return;
        }

        HasPlayback = false;
        CanControlPlayback = false;
        ArtworkUrl = null;
        DeviceName = string.Empty;
        Devices = [];
        SelectedDeviceId = null;
        Title = state == SpotifyConnectionState.Connecting
            ? localizer.GetText("Connecting")
            : localizer.GetText("NotConnected");
        Artist = state == SpotifyConnectionState.Connecting
            ? localizer.GetText("CompleteSignIn")
            : localizer.GetText("ConnectInSettings");
    }

    public void ApplyPlayback(SpotifyPlaybackSnapshot? snapshot,
        IReadOnlyList<SpotifyDevice>? availableDevices = null)
    {
        if (availableDevices is not null)
        {
            Devices = availableDevices;
        }

        if (snapshot is null)
        {
            ShowIdle();
            return;
        }

        applyingSnapshot = true;
        HasPlayback = true;
        CanControlPlayback = true;
        Title = snapshot.Title;
        Artist = snapshot.Artist;
        Album = snapshot.Album;
        ArtworkUrl = snapshot.ArtworkUrl;
        DeviceName = snapshot.Device?.Name ?? string.Empty;
        IsPlaying = snapshot.IsPlaying;
        ProgressMilliseconds = Math.Max(0, snapshot.Progress.TotalMilliseconds);
        DurationMilliseconds = Math.Max(1, snapshot.Duration.TotalMilliseconds);
        VolumePercent = Math.Clamp(snapshot.Device?.VolumePercent ?? 0, 0, 100);
        SelectedDeviceId = snapshot.Device?.Id;
        applyingSnapshot = false;
    }

    public void Previous() => Request(new SpotifyPlaybackAction(SpotifyPlaybackActionKind.Previous));

    public void TogglePlayback() => Request(new SpotifyPlaybackAction(SpotifyPlaybackActionKind.TogglePlayback));

    public void Next() => Request(new SpotifyPlaybackAction(SpotifyPlaybackActionKind.Next));

    public void Seek(double positionMilliseconds)
    {
        if (!applyingSnapshot)
        {
            Request(new SpotifyPlaybackAction(SpotifyPlaybackActionKind.Seek,
                TimeSpan.FromMilliseconds(Math.Clamp(positionMilliseconds, 0, DurationMilliseconds))));
        }
    }

    public void SetVolume(double volumePercent)
    {
        if (!applyingSnapshot)
        {
            Request(new SpotifyPlaybackAction(SpotifyPlaybackActionKind.SetVolume,
                VolumePercent: Math.Clamp((int)Math.Round(volumePercent), 0, 100)));
        }
    }

    public void Transfer(string? deviceId)
    {
        if (!applyingSnapshot && !string.IsNullOrWhiteSpace(deviceId))
        {
            Request(new SpotifyPlaybackAction(SpotifyPlaybackActionKind.Transfer,
                DeviceId: deviceId));
        }
    }

    public void Dispose()
    {
        messenger.UnregisterAll(this);
        GC.SuppressFinalize(this);
    }

    private void ShowIdle()
    {
        HasPlayback = false;
        CanControlPlayback = false;
        Title = localizer.GetText("NothingPlaying");
        Artist = localizer.GetText("StartPlayback");
        Album = string.Empty;
        ArtworkUrl = null;
        DeviceName = string.Empty;
        IsPlaying = false;
        ProgressMilliseconds = 0;
        DurationMilliseconds = 1;
    }

    private void Request(SpotifyPlaybackAction action)
    {
        if (CanControlPlayback)
        {
            PlaybackActionRequested?.Invoke(this, action);
        }
    }

    private static string FormatTime(double milliseconds)
    {
        TimeSpan value = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        return value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss")
            : value.ToString(@"m\:ss");
    }
}
