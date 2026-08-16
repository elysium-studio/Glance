using Glance.Application.Abstractions;
using Whisper.net;

namespace Glance.Transcription.Whisper;

public sealed class WhisperTranscriptionProvider(WhisperModelCatalog catalog) :
    ITranscriptionProvider,
    IDisposable
{
    public event EventHandler? StateChanged
    {
        add => catalog.StateChanged += value;
        remove => catalog.StateChanged -= value;
    }

    public string Id => "Whisper";

    public string DisplayName => "Whisper";

    public IReadOnlyList<TranscriptionModel> Models => catalog.Models;

    public string DefaultModelId => catalog.DefaultModelId;

    public bool IsInstalled(string modelId) => catalog.IsInstalled(modelId);

    public Task<TranscriptionModelState> GetStateAsync(string modelId,
        CancellationToken cancellationToken = default) => catalog.GetStateAsync(modelId, cancellationToken);

    public Task InstallAsync(string modelId,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default) => catalog.InstallAsync(modelId, progress, cancellationToken);

    public Task RemoveAsync(string modelId,
        CancellationToken cancellationToken = default) => catalog.RemoveAsync(modelId, cancellationToken);

    public TranscriptionModelDownload? GetDownload(string modelId)
    {
        BackgroundDownloadSnapshot? download = catalog.GetDownload(modelId);
        return download is null
            ? null
            : new TranscriptionModelDownload(MapStatus(download.Status),
                download.Progress,
                download.ErrorMessage);
    }

    public bool CancelInstall(string modelId) => catalog.CancelInstall(modelId);

    public TranscriptionAudioFormat GetAudioFormat(string modelId)
    {
        _ = catalog.GetModelPath(modelId);
        return TranscriptionAudioFormat.Speech;
    }

    public async Task<ITranscriptionDecoder> CreateDecoderAsync(string modelId,
        string language = "auto",
        CancellationToken cancellationToken = default)
    {
        if (await catalog.GetStateAsync(modelId, cancellationToken) != TranscriptionModelState.Installed)
        {
            throw new InvalidOperationException("The selected transcription model is not installed");
        }

        WhisperFactory factory = WhisperFactory.FromPath(catalog.GetModelPath(modelId));
        WhisperProcessor processor = factory.CreateBuilder()
            .WithLanguage(string.IsNullOrWhiteSpace(language) ? "auto" : language)
            .Build();
        return new WhisperTranscriptionDecoder(factory, processor);
    }

    public void Dispose() => catalog.Dispose();

    private static TranscriptionModelDownloadStatus MapStatus(BackgroundDownloadStatus status) => status switch
    {
        BackgroundDownloadStatus.Queued => TranscriptionModelDownloadStatus.Queued,
        BackgroundDownloadStatus.Downloading => TranscriptionModelDownloadStatus.Downloading,
        BackgroundDownloadStatus.Completed => TranscriptionModelDownloadStatus.Completed,
        BackgroundDownloadStatus.Cancelled => TranscriptionModelDownloadStatus.Cancelled,
        BackgroundDownloadStatus.Failed => TranscriptionModelDownloadStatus.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
