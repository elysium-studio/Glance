namespace Glance.Transcription;

public interface ITranscriptionModelCatalog
{
    event EventHandler? StateChanged;

    IReadOnlyList<TranscriptionModel> Models { get; }

    string DefaultModelId { get; }

    bool IsInstalled(string modelId);

    Task<TranscriptionModelState> GetStateAsync(string modelId,
        CancellationToken cancellationToken = default);

    Task InstallAsync(string modelId,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(string modelId,
        CancellationToken cancellationToken = default);

    TranscriptionModelDownload? GetDownload(string modelId);

    bool CancelInstall(string modelId);
}
