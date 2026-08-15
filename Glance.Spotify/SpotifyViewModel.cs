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
    public const uint DefaultAccentColor = 0xFF1ED760;

    private readonly IDispatcher dispatcher;
    private readonly ITextLocalizer localizer;
    private readonly IMessenger messenger;
    private bool applyingSnapshot;
    private double? pendingSeekOrigin;
    private double? pendingSeekTarget;
    private string? pendingSeekTrackId;
    private string? currentTrackId;

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
    private object? artwork;

    [ObservableProperty]
    private object? ambientArtwork;

    [ObservableProperty]
    private uint accentColor = DefaultAccentColor;

    [ObservableProperty]
    private uint backgroundForegroundColor = 0xFFFFFFFF;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackSourceName))]
    private string deviceName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackText))]
    [NotifyPropertyChangedFor(nameof(HasNoPlayback))]
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

    public bool HasNoPlayback => !HasPlayback;

    public double ProgressScale => Math.Clamp(ProgressMilliseconds / Math.Max(1, DurationMilliseconds), 0, 1);

    public string ProgressText => $"{FormatTime(ProgressMilliseconds)} / {FormatTime(DurationMilliseconds)}";

    public event EventHandler<SpotifyPlaybackAction>? PlaybackActionRequested;

    public void Receive(OptionsChangedEventArgs<SpotifySettings> message) =>
        dispatcher.Dispatch(() => ClientId = message.Options.ClientId);

    public void ApplyConnectionState(SpotifyConnectionState state, string? errorMessage = null) =>
        dispatcher.Dispatch(() => ApplyConnectionStateCore(state, errorMessage));

    public void ApplyPlayback(SpotifyPlaybackSnapshot? snapshot,
        IReadOnlyList<SpotifyDevice>? availableDevices = null) =>
        dispatcher.Dispatch(() => ApplyPlaybackCore(snapshot, availableDevices));

    public void ApplyDevices(IReadOnlyList<SpotifyDevice> availableDevices) =>
        dispatcher.Dispatch(() => Devices = availableDevices);

    public void AdvancePlayback(TimeSpan elapsed) => dispatcher.Dispatch(() =>
    {
        if (HasPlayback && IsPlaying && pendingSeekTarget is null)
        {
            ProgressMilliseconds = Math.Min(DurationMilliseconds,
                ProgressMilliseconds + Math.Max(0, elapsed.TotalMilliseconds));
        }
    });

    public void SetStatusMessage(string message) => dispatcher.Dispatch(() => StatusMessage = message);

    public void CancelPendingSeek() => dispatcher.Dispatch(ClearPendingSeek);

    private void ApplyConnectionStateCore(SpotifyConnectionState state, string? errorMessage)
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
        ClearPendingSeek();
        currentTrackId = null;
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

    private void ApplyPlaybackCore(SpotifyPlaybackSnapshot? snapshot,
        IReadOnlyList<SpotifyDevice>? availableDevices)
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
        DurationMilliseconds = Math.Max(1, snapshot.Duration.TotalMilliseconds);
        Title = snapshot.Title;
        Artist = snapshot.Artist;
        Album = snapshot.Album;
        ArtworkUrl = snapshot.ArtworkUrl;
        DeviceName = snapshot.Device?.Name ?? string.Empty;
        IsPlaying = snapshot.IsPlaying;
        ProgressMilliseconds = ResolveProgress(snapshot);
        VolumePercent = Math.Clamp(snapshot.Device?.VolumePercent ?? 0, 0, 100);
        SelectedDeviceId = snapshot.Device?.Id;
        currentTrackId = snapshot.TrackId;
        applyingSnapshot = false;
    }

    public void Previous() => Request(new SpotifyPlaybackAction(SpotifyPlaybackActionKind.Previous));

    public void TogglePlayback() => Request(new SpotifyPlaybackAction(SpotifyPlaybackActionKind.TogglePlayback));

    public void Next() => Request(new SpotifyPlaybackAction(SpotifyPlaybackActionKind.Next));

    public void Seek(double positionMilliseconds)
    {
        if (!applyingSnapshot)
        {
            double position = Math.Clamp(positionMilliseconds, 0, DurationMilliseconds);
            pendingSeekOrigin = ProgressMilliseconds;
            pendingSeekTarget = position;
            pendingSeekTrackId = currentTrackId;
            ProgressMilliseconds = position;
            Request(new SpotifyPlaybackAction(SpotifyPlaybackActionKind.Seek,
                TimeSpan.FromMilliseconds(position)));
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
        ClearPendingSeek();
        currentTrackId = null;
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

    private double ResolveProgress(SpotifyPlaybackSnapshot snapshot)
    {
        double progress = Math.Max(0, snapshot.Progress.TotalMilliseconds);

        if (pendingSeekOrigin is not double origin ||
            pendingSeekTarget is not double target)
        {
            return progress;
        }

        if (!string.Equals(pendingSeekTrackId, snapshot.TrackId, StringComparison.Ordinal))
        {
            ClearPendingSeek();
            return progress;
        }

        double midpoint = origin + ((target - origin) / 2);
        bool confirmed = target >= origin
            ? progress >= midpoint
            : progress <= midpoint;

        if (confirmed)
        {
            ClearPendingSeek();
            return progress;
        }

        return target;
    }

    private void ClearPendingSeek()
    {
        pendingSeekOrigin = null;
        pendingSeekTarget = null;
        pendingSeekTrackId = null;
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
