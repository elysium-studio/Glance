namespace Glance.Torrents;

public sealed class TorrentAddCoordinator(ITorrentEngineService engine) : IAsyncDisposable
{
    private readonly CancellationTokenSource disposalCancellation = new();
    private readonly ITorrentEngineService engine = engine;
    private readonly SemaphoreSlim synchronization = new(1, 1);
    private readonly HashSet<string> pendingSessions = new(StringComparer.OrdinalIgnoreCase);
    private int disposed;

    public async Task<TorrentMetadataSession> PrepareAsync(TorrentInput input,
        string downloadPath,
        TimeSpan magnetMetadataTimeout,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disposalCancellation.Token);
        TorrentMetadataSession session = await engine.ResolveMetadataAsync(input,
            downloadPath,
            magnetMetadataTimeout,
            linked.Token);
        await synchronization.WaitAsync(linked.Token);

        try
        {
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
        string downloadPath,
        CancellationToken cancellationToken = default)
    {
        if (selectedFiles.Count == 0)
        {
            throw new InvalidOperationException("Select at least one file to download.");
        }

        if (string.IsNullOrWhiteSpace(downloadPath))
        {
            throw new InvalidOperationException("Choose a download folder.");
        }

        await engine.ConfirmAsync(session.SessionId,
            selectedFiles,
            downloadPath,
            cancellationToken);
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

    private void Forget(TorrentMetadataSession session) => _ = pendingSessions.Remove(session.SessionId);
}
