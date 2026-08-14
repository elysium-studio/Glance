namespace Glance.Spotify;

public enum SpotifyConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

public enum SpotifyRepeatMode
{
    Off,
    Context,
    Track
}

public sealed record SpotifyConnectionResult(bool Succeeded, string? ErrorMessage = null);

public sealed record SpotifyAccount(string Id, string DisplayName);

public sealed record SpotifyDevice(string Id,
    string Name,
    string Type,
    bool IsActive,
    bool IsRestricted,
    int VolumePercent);

public sealed record SpotifyPlaybackSnapshot(string TrackId,
    string Title,
    string Artist,
    string Album,
    string? ArtworkUrl,
    TimeSpan Progress,
    TimeSpan Duration,
    bool IsPlaying,
    bool ShuffleEnabled,
    SpotifyRepeatMode RepeatMode,
    SpotifyDevice? Device);

public sealed class SpotifyConnectionStateChangedEventArgs(SpotifyConnectionState state,
    string? errorMessage = null) : EventArgs
{
    public SpotifyConnectionState State { get; } = state;

    public string? ErrorMessage { get; } = errorMessage;
}
