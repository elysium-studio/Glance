using Glance.Application.Abstractions;

namespace Glance.Transcription.Whisper;

public sealed class WhisperModelCatalog :
    IDisposable
{
    private const string Repository = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main";
    private const string DownloadIdPrefix = "transcription-model:Whisper/";
    private readonly IBackgroundDownloadManager downloads;
    private readonly string modelsDirectory;
    private readonly IReadOnlyDictionary<string, ModelFile> modelFiles;

    public WhisperModelCatalog(IBackgroundDownloadManager downloads,
        string modelsDirectory)
    {
        this.downloads = downloads;
        this.modelsDirectory = modelsDirectory;
        Models =
        [
            new TranscriptionModel("whisper-large-v3-turbo",
                "Whisper Large v3 Turbo",
                "Fast, highly accurate multilingual transcription",
                1624555275),
            new TranscriptionModel("whisper-large-v3",
                "Whisper Large v3",
                "Highest local accuracy for demanding transcription",
                3095033483),
            new TranscriptionModel("whisper-large-v3-turbo-q5",
                "Whisper Large v3 Turbo Compact",
                "A smaller, faster download with slightly reduced accuracy",
                574041195)
        ];
        modelFiles = new Dictionary<string, ModelFile>(StringComparer.OrdinalIgnoreCase)
        {
            ["whisper-large-v3-turbo"] = new("ggml-large-v3-turbo.bin"),
            ["whisper-large-v3"] = new("ggml-large-v3.bin"),
            ["whisper-large-v3-turbo-q5"] = new("ggml-large-v3-turbo-q5_0.bin")
        };
        downloads.DownloadChanged += HandleDownloadChanged;
    }

    public IReadOnlyList<TranscriptionModel> Models { get; }

    public string DefaultModelId => "whisper-large-v3-turbo";

    public event EventHandler? StateChanged;

    public bool IsInstalled(string modelId) => File.Exists(GetModelPath(modelId));

    public Task<TranscriptionModelState> GetStateAsync(string modelId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(IsInstalled(modelId)
            ? TranscriptionModelState.Installed
            : TranscriptionModelState.NotInstalled);
    }

    public async Task InstallAsync(string modelId,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ModelFile model = GetModelFile(modelId);
        string destinationPath = GetModelPath(modelId);

        if (File.Exists(destinationPath))
        {
            progress?.Report(1);
            return;
        }

        string downloadId = GetDownloadId(modelId);
        EventHandler<BackgroundDownloadChangedEventArgs> handler = (_, args) =>
        {
            if (string.Equals(args.Download.Id, downloadId, StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report(args.Download.Progress);
            }
        };
        downloads.DownloadChanged += handler;

        try
        {
            BackgroundDownloadSnapshot download = downloads.Enqueue(new BackgroundDownloadRequest(downloadId,
                new Uri($"{Repository}/{model.FileName}"),
                destinationPath));
            progress?.Report(download.Progress);
            BackgroundDownloadSnapshot completed = await downloads.WaitForCompletionAsync(downloadId,
                cancellationToken);

            switch (completed.Status)
            {
                case BackgroundDownloadStatus.Completed:
                    progress?.Report(1);
                    return;
                case BackgroundDownloadStatus.Cancelled:
                    throw new OperationCanceledException();
                case BackgroundDownloadStatus.Failed:
                    throw new IOException(completed.ErrorMessage ?? "The model download failed.");
                default:
                    throw new InvalidOperationException();
            }
        }
        finally
        {
            downloads.DownloadChanged -= handler;
        }
    }

    public Task RemoveAsync(string modelId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (GetDownload(modelId)?.IsActive == true)
        {
            throw new InvalidOperationException("The model is still downloading.");
        }

        string path = GetModelPath(modelId);

        if (File.Exists(path))
        {
            File.Delete(path);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }

    public string GetModelPath(string modelId) => Path.Combine(modelsDirectory, GetModelFile(modelId).FileName);

    public BackgroundDownloadSnapshot? GetDownload(string modelId) => downloads.GetDownload(GetDownloadId(modelId));

    public bool CancelInstall(string modelId) => downloads.Cancel(GetDownloadId(modelId));

    public void Dispose() => downloads.DownloadChanged -= HandleDownloadChanged;

    private void HandleDownloadChanged(object? sender, BackgroundDownloadChangedEventArgs args)
    {
        if (args.Download.Id.StartsWith(DownloadIdPrefix, StringComparison.OrdinalIgnoreCase))
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string GetDownloadId(string modelId) => $"{DownloadIdPrefix}{modelId}";

    private ModelFile GetModelFile(string modelId) => modelFiles.TryGetValue(modelId, out ModelFile? model)
        ? model
        : throw new ArgumentOutOfRangeException(nameof(modelId));

    private sealed record ModelFile(string FileName);
}
