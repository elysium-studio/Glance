namespace Glance.Spotify;

public enum SpotifyPlaybackActionKind
{
    Previous,
    TogglePlayback,
    Next,
    Seek,
    SetVolume,
    Transfer
}

public sealed record SpotifyPlaybackAction(SpotifyPlaybackActionKind Kind,
    TimeSpan Position = default,
    int VolumePercent = 0,
    string? DeviceId = null);
