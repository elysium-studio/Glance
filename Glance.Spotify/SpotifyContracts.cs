namespace Glance.Spotify;

public interface ISpotifyConnectionService
{
    SpotifyConnectionState State { get; }

    string? ConnectedClientId { get; }

    event EventHandler<SpotifyConnectionStateChangedEventArgs>? StateChanged;

    Task<SpotifyConnectionResult> ConnectAsync(string clientId,
        CancellationToken cancellationToken = default);

    Task<bool> RestoreAsync(string clientId,
        CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);
}

public interface ISpotifyProfileService
{
    Task<SpotifyAccount?> GetCurrentAccountAsync(CancellationToken cancellationToken = default);
}

public interface ISpotifyPlaybackService
{
    Task<SpotifyPlaybackSnapshot?> GetPlaybackAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SpotifyDevice>> GetDevicesAsync(CancellationToken cancellationToken = default);

    Task PreviousAsync(CancellationToken cancellationToken = default);

    Task ResumeAsync(CancellationToken cancellationToken = default);

    Task PauseAsync(CancellationToken cancellationToken = default);

    Task NextAsync(CancellationToken cancellationToken = default);

    Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default);

    Task SetVolumeAsync(int volumePercent, CancellationToken cancellationToken = default);

    Task TransferAsync(string deviceId, CancellationToken cancellationToken = default);
}
