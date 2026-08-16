using Glance.FoundryLocal;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;

namespace Glance.Transcription.Nemotron;

public sealed class NemotronTranscriptionProvider :
    ITranscriptionProvider,
    IAsyncDisposable
{
    private const string ModelId = "nemotron-3.5-asr-streaming-0.6b";
    private const long ModelSize = 792723456;
    private readonly ILogger<NemotronTranscriptionProvider> logger;
    private readonly SemaphoreSlim modelGate = new(1, 1);
    private readonly object downloadSynchronization = new();
    private readonly Task initialization;
    private IModel? model;
    private bool isInstalled;
    private bool isLoaded;
    private TranscriptionModelDownload? download;
    private CancellationTokenSource? downloadCancellation;
    private Task? downloadTask;
    private int disposed;

    public NemotronTranscriptionProvider(ILogger<NemotronTranscriptionProvider> logger)
    {
        this.logger = logger;
        Models =
        [
            new TranscriptionModel(ModelId,
                "Nemotron 3.5 ASR Streaming 0.6B",
                "Fast multilingual live transcription",
                ModelSize,
                true)
        ];
        initialization = Task.Run(InitializeAsync);
    }

    public event EventHandler? StateChanged;

    public string Id => "Nemotron";

    public string DisplayName => "NVIDIA Nemotron";

    public IReadOnlyList<TranscriptionModel> Models { get; }

    public string DefaultModelId => ModelId;

    public bool IsInstalled(string modelId)
    {
        ValidateModel(modelId);
        return isInstalled;
    }

    public async Task<TranscriptionModelState> GetStateAsync(string modelId,
        CancellationToken cancellationToken = default)
    {
        ValidateModel(modelId);
        await initialization.WaitAsync(cancellationToken);
        return isInstalled ? TranscriptionModelState.Installed : TranscriptionModelState.NotInstalled;
    }

    public async Task InstallAsync(string modelId,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateModel(modelId);
        await initialization.WaitAsync(cancellationToken);

        if (isInstalled)
        {
            progress?.Report(1);
            return;
        }

        Task activeDownload;

        lock (downloadSynchronization)
        {
            if (downloadTask is null || downloadTask.IsCompleted)
            {
                downloadCancellation?.Dispose();
                downloadCancellation = new CancellationTokenSource();
                downloadTask = DownloadAsync(downloadCancellation.Token);
            }

            activeDownload = downloadTask;
        }

        await activeDownload.WaitAsync(cancellationToken);
        progress?.Report(1);
    }

    public async Task RemoveAsync(string modelId,
        CancellationToken cancellationToken = default)
    {
        ValidateModel(modelId);
        await initialization.WaitAsync(cancellationToken);
        await modelGate.WaitAsync(cancellationToken);

        try
        {
            if (isLoaded)
            {
                await model!.UnloadAsync(cancellationToken);
                isLoaded = false;
            }

            if (isInstalled)
            {
                await model!.RemoveFromCacheAsync(cancellationToken);
                isInstalled = false;
                SetDownload(null);
            }
        }
        finally
        {
            _ = modelGate.Release();
        }
    }

    public TranscriptionModelDownload? GetDownload(string modelId)
    {
        ValidateModel(modelId);

        lock (downloadSynchronization)
        {
            return download;
        }
    }

    public bool CancelInstall(string modelId)
    {
        ValidateModel(modelId);

        lock (downloadSynchronization)
        {
            if (download?.IsActive != true || downloadCancellation is null)
            {
                return false;
            }

            downloadCancellation.Cancel();
            return true;
        }
    }

    public TranscriptionAudioFormat GetAudioFormat(string modelId)
    {
        ValidateModel(modelId);
        return TranscriptionAudioFormat.Speech;
    }

    public async Task<ITranscriptionDecoder> CreateDecoderAsync(string modelId,
        string language = "auto",
        CancellationToken cancellationToken = default)
    {
        ValidateModel(modelId);
        await initialization.WaitAsync(cancellationToken);

        if (!isInstalled)
        {
            throw new InvalidOperationException("The selected transcription model is not installed");
        }

        await modelGate.WaitAsync(cancellationToken);

        try
        {
            if (!isLoaded)
            {
                await model!.LoadAsync(cancellationToken);
                isLoaded = true;
            }

            return await NemotronTranscriptionDecoder.CreateAsync(
                await model!.GetAudioClientAsync(cancellationToken),
                language,
                cancellationToken);
        }
        finally
        {
            _ = modelGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        downloadCancellation?.Cancel();

        if (downloadTask is not null)
        {
            try
            {
                await downloadTask;
            }
            catch (Exception)
            {
            }
        }

        try
        {
            await initialization;
        }
        catch (Exception)
        {
        }

        await modelGate.WaitAsync();

        try
        {
            if (isLoaded && model is not null)
            {
                await model.UnloadAsync();
            }
        }
        finally
        {
            _ = modelGate.Release();
            modelGate.Dispose();
            downloadCancellation?.Dispose();
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            await FoundryLocalRuntime.EnsureInitializedAsync(logger);
            ICatalog catalog = await FoundryLocalManager.Instance.GetCatalogAsync();
            model = await catalog.GetModelAsync(ModelId) ?? throw new InvalidOperationException("Nemotron 3.5 ASR Streaming is unavailable in Microsoft Foundry Local");
            isInstalled = model is Model foundryModel && await foundryModel.IsCachedAsync();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to initialize the Nemotron transcription provider");
            throw;
        }
    }

    private async Task DownloadAsync(CancellationToken cancellationToken)
    {
        SetDownload(new TranscriptionModelDownload(TranscriptionModelDownloadStatus.Queued, 0));

        try
        {
            SetDownload(new TranscriptionModelDownload(TranscriptionModelDownloadStatus.Downloading, 0));
            await model!.DownloadAsync(value => SetDownload(new TranscriptionModelDownload(
                TranscriptionModelDownloadStatus.Downloading,
                Math.Clamp(value / 100d, 0, 1))), cancellationToken);
            isInstalled = true;
            SetDownload(new TranscriptionModelDownload(TranscriptionModelDownloadStatus.Completed, 1));
        }
        catch (OperationCanceledException)
        {
            SetDownload(new TranscriptionModelDownload(TranscriptionModelDownloadStatus.Cancelled, download?.Progress ?? 0));
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to download the Nemotron transcription model");
            SetDownload(new TranscriptionModelDownload(TranscriptionModelDownloadStatus.Failed,
                download?.Progress ?? 0,
                exception.Message));
            throw;
        }
    }

    private void SetDownload(TranscriptionModelDownload? value)
    {
        lock (downloadSynchronization)
        {
            download = value;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void ValidateModel(string modelId)
    {
        if (!string.Equals(modelId, ModelId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentOutOfRangeException(nameof(modelId));
        }
    }
}
