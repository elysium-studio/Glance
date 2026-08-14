using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;

namespace Glance.Spotify.WinUI;

internal interface ISpotifyBrowserLauncher
{
    Task<bool> LaunchAsync(Uri uri);
}

internal sealed class SpotifyBrowserLauncher : ISpotifyBrowserLauncher
{
    public async Task<bool> LaunchAsync(Uri uri) => await Launcher.LaunchUriAsync(uri);
}

internal interface ISpotifyAuthorizationBroker
{
    Task<SpotifyAuthorizationGrant> AuthorizeAsync(string clientId,
        CancellationToken cancellationToken = default);
}

internal sealed class SpotifyAuthorizationBroker(ISpotifyLoopbackServerFactory loopbackServerFactory,
    ISpotifyBrowserLauncher browserLauncher) : ISpotifyAuthorizationBroker
{
    private static readonly Uri AuthorizationEndpoint = new("https://accounts.spotify.com/authorize");
    private const string Scopes = "user-read-playback-state user-read-currently-playing user-modify-playback-state user-read-private";

    public async Task<SpotifyAuthorizationGrant> AuthorizeAsync(string clientId,
        CancellationToken cancellationToken = default)
    {
        await using ISpotifyLoopbackServer server = loopbackServerFactory.Create();
        string verifier = CreateRandomValue(64);
        string challenge = ToBase64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        string state = CreateRandomValue(32);
        Uri authorizationUri = CreateAuthorizationUri(clientId,
            server.RedirectUri,
            state,
            challenge);

        if (!await browserLauncher.LaunchAsync(authorizationUri))
        {
            throw new SpotifyAuthenticationException("The Spotify sign-in page could not be opened.");
        }

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        SpotifyLoopbackResult result = await server.WaitForResultAsync(timeout.Token);

        if (!string.Equals(result.State, state, StringComparison.Ordinal) ||
            !string.IsNullOrWhiteSpace(result.Error) ||
            string.IsNullOrWhiteSpace(result.Code))
        {
            throw new SpotifyAuthenticationException("Spotify sign-in was cancelled or could not be verified.");
        }

        return new SpotifyAuthorizationGrant(result.Code, verifier, server.RedirectUri);
    }

    private static Uri CreateAuthorizationUri(string clientId,
        Uri redirectUri,
        string state,
        string challenge)
    {
        Dictionary<string, string> values = new()
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri.AbsoluteUri,
            ["scope"] = Scopes,
            ["state"] = state,
            ["code_challenge_method"] = "S256",
            ["code_challenge"] = challenge
        };
        string query = string.Join('&', values.Select(value =>
            $"{Uri.EscapeDataString(value.Key)}={Uri.EscapeDataString(value.Value)}"));
        return new UriBuilder(AuthorizationEndpoint) { Query = query }.Uri;
    }

    private static string CreateRandomValue(int byteCount)
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(byteCount);
        return ToBase64Url(bytes);
    }

    private static string ToBase64Url(byte[] bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}
