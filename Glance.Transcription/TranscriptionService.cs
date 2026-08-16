namespace Glance.Transcription;

public sealed class TranscriptionService :
    ITranscriptionModelCatalog,
    ITranscriptionDecoderFactory,
    ITranscriptionProviderRegistry,
    IDisposable
{
    private readonly object synchronization = new();
    private readonly List<ITranscriptionProvider> providers = [];
    private int disposed;

    public event EventHandler? StateChanged;

    public IReadOnlyList<TranscriptionModel> Models
    {
        get
        {
            lock (synchronization)
            {
                return [.. providers.SelectMany(provider => provider.Models.Select(model => ToCatalogModel(provider, model)))];
            }
        }
    }

    public string DefaultModelId
    {
        get
        {
            IReadOnlyList<TranscriptionModel> models = Models;
            return models.FirstOrDefault(model => model.IsRecommended)?.Id ??
                models.FirstOrDefault()?.Id ??
                string.Empty;
        }
    }

    public IDisposable Register(ITranscriptionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

        if (string.IsNullOrWhiteSpace(provider.Id) ||
            provider.Models.Any(model => string.IsNullOrWhiteSpace(model.Id)) ||
            provider.Models.Select(model => model.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != provider.Models.Count)
        {
            throw new InvalidOperationException("A transcription provider must expose a unique identifier and unique, non-empty model identifiers.");
        }

        lock (synchronization)
        {
            if (providers.Any(candidate => string.Equals(candidate.Id, provider.Id, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A transcription provider with the identifier '{provider.Id}' is already registered.");
            }

            providers.Add(provider);
            provider.StateChanged += HandleProviderStateChanged;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
        return new Registration(this, provider);
    }

    public bool IsInstalled(string modelId)
    {
        ProviderModel selection = GetProviderModel(modelId);
        return selection.Provider.IsInstalled(selection.Model.Id);
    }

    public Task<TranscriptionModelState> GetStateAsync(string modelId,
        CancellationToken cancellationToken = default)
    {
        ProviderModel selection = GetProviderModel(modelId);
        return selection.Provider.GetStateAsync(selection.Model.Id, cancellationToken);
    }

    public Task InstallAsync(string modelId,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ProviderModel selection = GetProviderModel(modelId);
        return selection.Provider.InstallAsync(selection.Model.Id, progress, cancellationToken);
    }

    public Task RemoveAsync(string modelId,
        CancellationToken cancellationToken = default)
    {
        ProviderModel selection = GetProviderModel(modelId);
        return selection.Provider.RemoveAsync(selection.Model.Id, cancellationToken);
    }

    public TranscriptionModelDownload? GetDownload(string modelId)
    {
        ProviderModel selection = GetProviderModel(modelId);
        return selection.Provider.GetDownload(selection.Model.Id);
    }

    public bool CancelInstall(string modelId)
    {
        ProviderModel selection = GetProviderModel(modelId);
        return selection.Provider.CancelInstall(selection.Model.Id);
    }

    public TranscriptionAudioFormat GetAudioFormat(string modelId)
    {
        ProviderModel selection = GetProviderModel(modelId);
        return selection.Provider.GetAudioFormat(selection.Model.Id);
    }

    public Task<ITranscriptionDecoder> CreateDecoderAsync(string modelId,
        string language = "auto",
        CancellationToken cancellationToken = default)
    {
        ProviderModel selection = GetProviderModel(modelId);
        return selection.Provider.CreateDecoderAsync(selection.Model.Id, language, cancellationToken);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        lock (synchronization)
        {
            foreach (ITranscriptionProvider provider in providers)
            {
                provider.StateChanged -= HandleProviderStateChanged;
            }

            providers.Clear();
        }
    }

    private ProviderModel GetProviderModel(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("A transcription model must be selected", nameof(modelId));
        }

        lock (synchronization)
        {
            foreach (ITranscriptionProvider provider in providers)
            {
                foreach (TranscriptionModel model in provider.Models)
                {
                    if (string.Equals(GetCatalogModelId(provider.Id, model.Id), modelId, StringComparison.OrdinalIgnoreCase))
                    {
                        return new ProviderModel(provider, model);
                    }
                }
            }
        }

        throw new ArgumentOutOfRangeException(nameof(modelId));
    }

    private void Unregister(ITranscriptionProvider provider)
    {
        bool removed;

        lock (synchronization)
        {
            removed = providers.Remove(provider);

            if (removed)
            {
                provider.StateChanged -= HandleProviderStateChanged;
            }
        }

        if (removed)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void HandleProviderStateChanged(object? sender, EventArgs args) => StateChanged?.Invoke(this, EventArgs.Empty);

    private static TranscriptionModel ToCatalogModel(ITranscriptionProvider provider,
        TranscriptionModel model) => model with
        {
            Id = GetCatalogModelId(provider.Id, model.Id),
            ProviderId = provider.Id,
            ProviderDisplayName = provider.DisplayName
        };

    private static string GetCatalogModelId(string providerId,
        string modelId) => $"{providerId}/{modelId}";

    private sealed record ProviderModel(ITranscriptionProvider Provider,
        TranscriptionModel Model);

    private sealed class Registration(TranscriptionService owner,
        ITranscriptionProvider provider) :
        IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                owner.Unregister(provider);
            }
        }
    }
}
