namespace Glance.Transcription;

public sealed record TranscriptionModel(string Id,
    string DisplayName,
    string Description,
    long DownloadSize,
    bool IsRecommended = false,
    string? ProviderId = null,
    string? ProviderDisplayName = null);
