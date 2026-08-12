namespace Glance.Torrents;

public interface ITorrentEngineService : IAsyncDisposable
{
    event EventHandler<TorrentSnapshotEventArgs>? SnapshotUpdated;

    event EventHandler<TorrentCompletedEventArgs>? TorrentCompleted;

    IReadOnlyCollection<string> ActiveTorrentIds { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<TorrentMetadataSession> ResolveMetadataAsync(TorrentInput input,
        string downloadPath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    Task ConfirmAsync(string sessionId,
        IReadOnlyCollection<string> selectedFiles,
        CancellationToken cancellationToken = default);

    Task CancelMetadataAsync(string sessionId);

    Task PauseAsync(string torrentId);

    Task ResumeAsync(string torrentId);

    Task RemoveAsync(string torrentId, bool deleteData);

    Task ApplySettingsAsync(TorrentSettings settings, CancellationToken cancellationToken = default);
}
