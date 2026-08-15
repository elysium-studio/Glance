namespace Glance.Application.Abstractions;

public interface IBackgroundDownloadManager
{
    event EventHandler<BackgroundDownloadChangedEventArgs>? DownloadChanged;

    IReadOnlyList<BackgroundDownloadSnapshot> Downloads { get; }

    BackgroundDownloadSnapshot Enqueue(BackgroundDownloadRequest request);

    BackgroundDownloadSnapshot? GetDownload(string id);

    Task<BackgroundDownloadSnapshot> WaitForCompletionAsync(string id,
        CancellationToken cancellationToken = default);

    bool Cancel(string id);

    bool Remove(string id);
}

public sealed record BackgroundDownloadRequest(string Id,
    Uri Source,
    string DestinationPath,
    string? TemporaryPath = null);

public sealed record BackgroundDownloadSnapshot(string Id,
    Uri Source,
    string DestinationPath,
    BackgroundDownloadStatus Status,
    long BytesReceived,
    long? TotalBytes,
    string? ErrorMessage)
{
    public double Progress => TotalBytes is > 0
        ? Math.Clamp((double)BytesReceived / TotalBytes.Value, 0, 1)
        : 0;

    public bool IsActive => Status is BackgroundDownloadStatus.Queued or
        BackgroundDownloadStatus.Downloading;
}

public sealed class BackgroundDownloadChangedEventArgs(BackgroundDownloadSnapshot download) :
    EventArgs
{
    public BackgroundDownloadSnapshot Download { get; } = download;
}

public enum BackgroundDownloadStatus
{
    Queued,
    Downloading,
    Completed,
    Cancelled,
    Failed
}
