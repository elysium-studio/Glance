using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Spotify.WinUI;

internal interface ISpotifyLoopbackServer : IAsyncDisposable
{
    Uri RedirectUri { get; }

    Task<SpotifyLoopbackResult> WaitForResultAsync(CancellationToken cancellationToken = default);
}

internal interface ISpotifyLoopbackServerFactory
{
    ISpotifyLoopbackServer Create();
}

internal sealed class SpotifyLoopbackServerFactory : ISpotifyLoopbackServerFactory
{
    public ISpotifyLoopbackServer Create() => new SpotifyLoopbackServer();
}

internal sealed class SpotifyLoopbackServer : ISpotifyLoopbackServer
{
    private readonly TcpListener listener = new(IPAddress.Loopback, SpotifyAuthenticationDefaults.LoopbackPort);
    private int disposed;

    public SpotifyLoopbackServer()
    {
        listener.Start(1);
    }

    public Uri RedirectUri => SpotifyAuthenticationDefaults.RedirectUri;

    public async Task<SpotifyLoopbackResult> WaitForResultAsync(CancellationToken cancellationToken = default)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using NetworkStream stream = client.GetStream();
        using StreamReader reader = new(stream, Encoding.ASCII, false, 1024, true);
        string? requestLine = await reader.ReadLineAsync(cancellationToken);

        while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellationToken)))
        {
        }

        SpotifyLoopbackResult result = ParseRequest(requestLine);
        string message = result.Error is null
            ? "Spotify is connected. You can return to Glance."
            : "Spotify could not be connected. You can return to Glance.";
        byte[] content = Encoding.UTF8.GetBytes($"<!doctype html><html><head><meta charset=\"utf-8\"><title>Glance</title></head><body style=\"font-family:Segoe UI,sans-serif;padding:32px\"><h1>Glance</h1><p>{message}</p></body></html>");
        string headers = $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {content.Length}\r\nConnection: close\r\n\r\n";
        byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
        await stream.WriteAsync(headerBytes, cancellationToken);
        await stream.WriteAsync(content, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        return result;
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            listener.Stop();
        }

        return ValueTask.CompletedTask;
    }

    private static SpotifyLoopbackResult ParseRequest(string? requestLine)
    {
        string[] parts = requestLine?.Split(' ', 3) ?? [];

        if (parts.Length < 2 || !Uri.TryCreate("http://127.0.0.1" + parts[1], UriKind.Absolute, out Uri? uri))
        {
            return new SpotifyLoopbackResult(null, null, "invalid_response");
        }

        Dictionary<string, string> values = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Split('=', 2))
            .ToDictionary(value => Uri.UnescapeDataString(value[0]),
                value => value.Length > 1 ? Uri.UnescapeDataString(value[1].Replace('+', ' ')) : string.Empty,
                StringComparer.Ordinal);
        values.TryGetValue("code", out string? code);
        values.TryGetValue("state", out string? state);
        values.TryGetValue("error", out string? error);
        return new SpotifyLoopbackResult(code, state, error);
    }
}
