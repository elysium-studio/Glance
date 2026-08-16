namespace Glance.Transcription;

public sealed record TranscriptionModelDownload(TranscriptionModelDownloadStatus Status,
    double Progress,
    string? ErrorMessage = null)
{
    public bool IsActive => Status is TranscriptionModelDownloadStatus.Queued or
        TranscriptionModelDownloadStatus.Downloading;
}

public enum TranscriptionModelDownloadStatus
{
    Queued,
    Downloading,
    Completed,
    Cancelled,
    Failed
}
