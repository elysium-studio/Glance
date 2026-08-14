using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Spotify.WinUI;

internal sealed class SpotifyAuthenticationException(string message) : Exception(message);

internal sealed class SpotifyOAuthClient(HttpClient httpClient)
{
    private static readonly Uri TokenEndpoint = new("https://accounts.spotify.com/api/token");

    public Task<SpotifyAccessToken> ExchangeAsync(string clientId,
        SpotifyAuthorizationGrant grant,
        CancellationToken cancellationToken = default) => RequestTokenAsync(clientId,
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = grant.Code,
                ["redirect_uri"] = grant.RedirectUri.AbsoluteUri,
                ["code_verifier"] = grant.CodeVerifier,
                ["client_id"] = clientId
            },
            null,
            cancellationToken);

    public Task<SpotifyAccessToken> RefreshAsync(string clientId,
        string refreshToken,
        CancellationToken cancellationToken = default) => RequestTokenAsync(clientId,
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = clientId
            },
            refreshToken,
            cancellationToken);

    private async Task<SpotifyAccessToken> RequestTokenAsync(string clientId,
        IReadOnlyDictionary<string, string> values,
        string? existingRefreshToken,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(values)
        };
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        string json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new SpotifyAuthenticationException("Spotify rejected the authorization request.");
        }

        SpotifyTokenResponse? payload = JsonSerializer.Deserialize(json,
            SpotifyJsonContext.Default.SpotifyTokenResponse);

        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new SpotifyAuthenticationException("Spotify returned an invalid authorization response.");
        }

        string refreshToken = payload.RefreshToken ?? existingRefreshToken ?? string.Empty;

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new SpotifyAuthenticationException("Spotify did not return a refresh token.");
        }

        return new SpotifyAccessToken(payload.AccessToken,
            DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, payload.ExpiresIn)),
            refreshToken);
    }
}
