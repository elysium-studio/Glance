using Glance.Spotify;
using System.Text.Json.Serialization;

namespace Glance.Spotify.WinUI;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SpotifySettings))]
[JsonSerializable(typeof(SpotifyTokenResponse))]
[JsonSerializable(typeof(SpotifyProfileResponse))]
[JsonSerializable(typeof(SpotifyPlaybackResponse))]
[JsonSerializable(typeof(SpotifyDevicesResponse))]
[JsonSerializable(typeof(SpotifyTransferRequest))]
internal sealed partial class SpotifyJsonContext : JsonSerializerContext;
