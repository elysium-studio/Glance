namespace Glance.Torrents;

public enum TorrentDownloadState
{
    Queued,
    RetrievingMetadata,
    Checking,
    Downloading,
    Paused,
    Seeding,
    Completed,
    Stopped,
    Error
}

public enum TorrentSeedingLimitMode
{
    Either,
    Both
}

public sealed record TorrentMetadataFile(string Path, long Size, bool IsSelected = true);

public sealed record TorrentMetadataSession(string SessionId,
    string TorrentId,
    TorrentInput Input,
    string Name,
    long TotalSize,
    IReadOnlyList<TorrentMetadataFile> Files,
    IReadOnlyList<string> Trackers,
    string DownloadPath);

public sealed record TorrentTransferSnapshot(string Id,
    string Name,
    TorrentDownloadState State,
    double Progress,
    long DownloadSpeed,
    long UploadSpeed,
    int PeerCount,
    long BytesDownloaded,
    long BytesUploaded,
    long TotalSize,
    TimeSpan SeedingTime,
    string? ErrorMessage = null);

public sealed record TorrentPersistedDownload(string Id,
    TorrentInput Input,
    string DownloadPath,
    IReadOnlyList<string> SelectedFiles,
    bool WasPaused,
    bool CompletionNotified);

public sealed record TorrentStateDocument(IReadOnlyList<TorrentPersistedDownload> Downloads);

public sealed class TorrentSnapshotEventArgs(TorrentTransferSnapshot snapshot) : EventArgs
{
    public TorrentTransferSnapshot Snapshot { get; } = snapshot;
}

public sealed class TorrentCompletedEventArgs(string torrentId) : EventArgs
{
    public string TorrentId { get; } = torrentId;
}
