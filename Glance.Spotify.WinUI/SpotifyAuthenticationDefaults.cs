using System;

namespace Glance.Spotify.WinUI;

internal static class SpotifyAuthenticationDefaults
{
    public const int LoopbackPort = 43821;

    public static Uri RedirectUri { get; } = new($"http://127.0.0.1:{LoopbackPort}/callback");
}
