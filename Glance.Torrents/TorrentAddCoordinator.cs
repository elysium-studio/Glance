namespace Glance.Torrents;

public sealed class TorrentAddCoordinator(ITorrentEngineService engine) : IAsyncDisposable
{
    private readonly CancellationTokenSource disposalCancellation = new();
    private readonly ITorrentEngineService engine = engine;
    private readonly SemaphoreSlim synchronization = new(1, 1);
    private readonly HashSet<string> pendingTorrentIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> pendingSessions = new(StringComparer.OrdinalIgnoreCase);
    private int disposed;

    public async Task<TorrentMetadataSession> PrepareAsync(TorrentInput input,
        string downloadPath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disposalCancellation.Token);
        TorrentMetadataSession session = await engine.ResolveMetadataAsync(input, downloadPath, timeout, linked.Token);
        await synchronization.WaitAsync(linked.Token);

        try
        {
            if (engine.ActiveTorrentIds.Contains(session.TorrentId, StringComparer.OrdinalIgnoreCase) ||
                !pendingTorrentIds.Add(session.TorrentId))
            {
                await engine.CancelMetadataAsync(session.SessionId);
                throw new InvalidOperationException("This torrent is already in Glance.");
            }

            _ = pendingSessions.Add(session.SessionId);
            return session;
        }
        finally
        {
            _ = synchronization.Release();
        }
    }

    public async Task ConfirmAsync(TorrentMetadataSession session,
        IReadOnlyCollection<string> selectedFiles,
        CancellationToken cancellationToken = default)
    {
        if (selectedFiles.Count == 0)
        {
            throw new InvalidOperationException("Select at least one file to download.");
        }

        await engine.ConfirmAsync(session.SessionId, selectedFiles, cancellationToken);
        Forget(session);
    }

    public async Task CancelAsync(TorrentMetadataSession session)
    {
        await engine.CancelMetadataAsync(session.SessionId);
        Forget(session);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        disposalCancellation.Cancel();
        string[] sessions;
        await synchronization.WaitAsync();

        try
        {
            sessions = [.. pendingSessions];
            pendingSessions.Clear();
            pendingTorrentIds.Clear();
        }
        finally
        {
            _ = synchronization.Release();
        }

        foreach (string session in sessions)
        {
            await engine.CancelMetadataAsync(session);
        }

        disposalCancellation.Dispose();
        synchronization.Dispose();
    }

    private void Forget(TorrentMetadataSession session)
    {
        _ = pendingSessions.Remove(session.SessionId);
        _ = pendingTorrentIds.Remove(session.TorrentId);
    }
}
