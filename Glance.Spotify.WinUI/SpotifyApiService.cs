using Glance.Spotify;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Spotify.WinUI;

internal sealed class SpotifyApiException(HttpStatusCode statusCode,
    string message,
    TimeSpan? retryAfter = null) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public TimeSpan? RetryAfter { get; } = retryAfter;
}

internal sealed class SpotifyApiService(HttpClient httpClient,
    ISpotifyAccessTokenProvider tokenProvider) :
    ISpotifyPlaybackService,
    ISpotifyProfileService
{
    private static readonly Uri ApiRoot = new("https://api.spotify.com/v1/");

    public async Task<SpotifyAccount?> GetCurrentAccountAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get,
            "me",
            null,
            cancellationToken);
        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        SpotifyProfileResponse? profile = JsonSerializer.Deserialize(json,
            SpotifyJsonContext.Default.SpotifyProfileResponse);
        return profile is null
            ? null
            : new SpotifyAccount(profile.Id,
                string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.Id : profile.DisplayName);
    }

    public async Task<SpotifyPlaybackSnapshot?> GetPlaybackAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get,
            "me/player",
            null,
            cancellationToken,
            HttpStatusCode.NoContent);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        SpotifyPlaybackResponse? playback = JsonSerializer.Deserialize(json,
            SpotifyJsonContext.Default.SpotifyPlaybackResponse);
        SpotifyTrackResponse? track = playback?.Item;

        if (playback is null || track is null)
        {
            return null;
        }

        string artist = string.Join(", ", track.Artists
            .Select(value => value.Name)
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        SpotifyImageResponse? image = track.Album?.Images
            .OrderBy(value => Math.Abs((value.Width ?? 300) - 300))
            .FirstOrDefault();
        return new SpotifyPlaybackSnapshot(track.Id ?? track.Name,
            track.Name,
            artist,
            track.Album?.Name ?? string.Empty,
            image?.Url,
            TimeSpan.FromMilliseconds(Math.Max(0, playback.ProgressMilliseconds ?? 0)),
            TimeSpan.FromMilliseconds(Math.Max(0, track.DurationMilliseconds)),
            playback.IsPlaying,
            playback.ShuffleState,
            ParseRepeatMode(playback.RepeatState),
            ToDevice(playback.Device));
    }

    public async Task<IReadOnlyList<SpotifyDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get,
            "me/player/devices",
            null,
            cancellationToken);
        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        SpotifyDevicesResponse? devices = JsonSerializer.Deserialize(json,
            SpotifyJsonContext.Default.SpotifyDevicesResponse);
        return devices?.Devices
            .Select(ToDevice)
            .Where(device => device is not null)
            .Cast<SpotifyDevice>()
            .ToArray() ?? [];
    }

    public Task PreviousAsync(CancellationToken cancellationToken = default) =>
        SendWithoutResponseAsync(HttpMethod.Post, "me/player/previous", null, cancellationToken);

    public Task ResumeAsync(CancellationToken cancellationToken = default) =>
        SendWithoutResponseAsync(HttpMethod.Put,
            "me/player/play",
            new StringContent("{}", Encoding.UTF8, "application/json"),
            cancellationToken);

    public Task PauseAsync(CancellationToken cancellationToken = default) =>
        SendWithoutResponseAsync(HttpMethod.Put, "me/player/pause", null, cancellationToken);

    public Task NextAsync(CancellationToken cancellationToken = default) =>
        SendWithoutResponseAsync(HttpMethod.Post, "me/player/next", null, cancellationToken);

    public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) =>
        SendWithoutResponseAsync(HttpMethod.Put,
            $"me/player/seek?position_ms={Math.Max(0, (long)position.TotalMilliseconds)}",
            null,
            cancellationToken);

    public Task SetVolumeAsync(int volumePercent, CancellationToken cancellationToken = default) =>
        SendWithoutResponseAsync(HttpMethod.Put,
            $"me/player/volume?volume_percent={Math.Clamp(volumePercent, 0, 100)}",
            null,
            cancellationToken);

    public Task TransferAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        SpotifyTransferRequest payload = new()
        {
            DeviceIds = [deviceId],
            Play = false
        };
        string json = JsonSerializer.Serialize(payload, SpotifyJsonContext.Default.SpotifyTransferRequest);
        return SendWithoutResponseAsync(HttpMethod.Put,
            "me/player",
            new StringContent(json, Encoding.UTF8, "application/json"),
            cancellationToken);
    }

    private async Task SendWithoutResponseAsync(HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using (content)
        using (HttpResponseMessage response = await SendAsync(method, path, content, cancellationToken))
        {
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken,
        params HttpStatusCode[] additionalSuccessStatuses)
    {
        byte[]? contentBytes = content is null
            ? null
            : await content.ReadAsByteArrayAsync(cancellationToken);
        string? contentType = content?.Headers.ContentType?.MediaType;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            string token = await tokenProvider.GetAccessTokenAsync(cancellationToken);
            using HttpRequestMessage request = new(method, new Uri(ApiRoot, path))
            {
                Content = CreateContent(contentBytes, contentType)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode || additionalSuccessStatuses.Contains(response.StatusCode))
            {
                return response;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                response.Dispose();
                tokenProvider.InvalidateAccessToken();
                continue;
            }

            TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;
            HttpStatusCode statusCode = response.StatusCode;
            response.Dispose();
            throw new SpotifyApiException(statusCode,
                statusCode == HttpStatusCode.Forbidden
                    ? "Spotify Premium and permission to control playback are required."
                    : "Spotify could not complete the request.",
                retryAfter);
        }

        throw new SpotifyApiException(HttpStatusCode.Unauthorized, "Spotify needs to be reconnected.");
    }

    private static HttpContent? CreateContent(byte[]? content,
        string? contentType)
    {
        if (content is null)
        {
            return null;
        }

        ByteArrayContent result = new(content);

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            result.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        }

        return result;
    }

    private static SpotifyDevice? ToDevice(SpotifyDeviceResponse? device) =>
        device is null || string.IsNullOrWhiteSpace(device.Id)
            ? null
            : new SpotifyDevice(device.Id,
                device.Name,
                device.Type,
                device.IsActive,
                device.IsRestricted,
                Math.Clamp(device.VolumePercent ?? 0, 0, 100));

    private static SpotifyRepeatMode ParseRepeatMode(string value) => value switch
    {
        "context" => SpotifyRepeatMode.Context,
        "track" => SpotifyRepeatMode.Track,
        _ => SpotifyRepeatMode.Off
    };
}
