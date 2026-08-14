using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;

namespace Glance.Spotify.WinUI;

internal sealed class SpotifyTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }
}

internal sealed class SpotifyProfileResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
}

internal sealed class SpotifyPlaybackResponse
{
    [JsonPropertyName("is_playing")]
    public bool IsPlaying { get; set; }

    [JsonPropertyName("progress_ms")]
    public int? ProgressMilliseconds { get; set; }

    [JsonPropertyName("shuffle_state")]
    public bool ShuffleState { get; set; }

    [JsonPropertyName("repeat_state")]
    public string RepeatState { get; set; } = "off";

    [JsonPropertyName("device")]
    public SpotifyDeviceResponse? Device { get; set; }

    [JsonPropertyName("item")]
    public SpotifyTrackResponse? Item { get; set; }
}

internal sealed class SpotifyTrackResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("duration_ms")]
    public int DurationMilliseconds { get; set; }

    [JsonPropertyName("artists")]
    public List<SpotifyArtistResponse> Artists { get; set; } = [];

    [JsonPropertyName("album")]
    public SpotifyAlbumResponse? Album { get; set; }
}

internal sealed class SpotifyArtistResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

internal sealed class SpotifyAlbumResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("images")]
    public List<SpotifyImageResponse> Images { get; set; } = [];
}

internal sealed class SpotifyImageResponse
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int? Width { get; set; }
}

internal sealed class SpotifyDevicesResponse
{
    [JsonPropertyName("devices")]
    public List<SpotifyDeviceResponse> Devices { get; set; } = [];
}

internal sealed class SpotifyDeviceResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("is_restricted")]
    public bool IsRestricted { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("volume_percent")]
    public int? VolumePercent { get; set; }
}

internal sealed class SpotifyTransferRequest
{
    [JsonPropertyName("device_ids")]
    public string[] DeviceIds { get; set; } = [];

    [JsonPropertyName("play")]
    public bool Play { get; set; }
}

internal sealed record SpotifyAccessToken(string Value,
    DateTimeOffset ExpiresAt,
    string RefreshToken);

internal sealed record SpotifyStoredCredential(string ClientId,
    string RefreshToken);

internal sealed record SpotifyAuthorizationGrant(string Code,
    string CodeVerifier,
    Uri RedirectUri);

internal sealed record SpotifyLoopbackResult(string? Code,
    string? State,
    string? Error);
