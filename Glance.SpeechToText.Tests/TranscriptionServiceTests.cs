using Glance.Application.Abstractions;
using Glance.Transcription;
using System.Runtime.CompilerServices;

namespace Glance.SpeechToText.Tests;

public sealed class TranscriptionServiceTests
{
    [Fact]
    public void RecommendedProviderBecomesDefault()
    {
        using TranscriptionService service = new();
        using IDisposable whisper = service.Register(new FakeProvider("Whisper",
            new TranscriptionModel("whisper", "Whisper", "Fallback", 1)));
        using IDisposable nemotron = service.Register(new FakeProvider("FoundryLocal",
            new TranscriptionModel("nemotron", "Nemotron", "Streaming", 1, true)));

        Assert.Equal("FoundryLocal/nemotron", service.DefaultModelId);
        Assert.Equal(["Whisper/whisper", "FoundryLocal/nemotron"], service.Models.Select(model => model.Id));
    }

    [Fact]
    public async Task SessionCreationRoutesToOwningProvider()
    {
        using TranscriptionService service = new();
        FakeProvider whisper = new("Whisper",
            new TranscriptionModel("whisper", "Whisper", "Fallback", 1));
        FakeProvider nemotron = new("FoundryLocal",
            new TranscriptionModel("nemotron", "Nemotron", "Streaming", 1, true));
        using IDisposable whisperRegistration = service.Register(whisper);
        using IDisposable nemotronRegistration = service.Register(nemotron);

        await using ITranscriptionDecoder decoder = await service.CreateDecoderAsync("FoundryLocal/nemotron");

        Assert.Equal(0, whisper.DecoderCount);
        Assert.Equal(1, nemotron.DecoderCount);
    }

    [Fact]
    public void RemovingProviderLeavesOtherProviderAvailable()
    {
        using TranscriptionService service = new();
        IDisposable whisper = service.Register(new FakeProvider("Whisper",
            new TranscriptionModel("whisper", "Whisper", "Fallback", 1)));
        IDisposable nemotron = service.Register(new FakeProvider("FoundryLocal",
            new TranscriptionModel("nemotron", "Nemotron", "Streaming", 1, true)));

        nemotron.Dispose();

        Assert.Equal("Whisper/whisper", service.DefaultModelId);
        Assert.Equal("Whisper/whisper", Assert.Single(service.Models).Id);
        whisper.Dispose();
    }

    [Fact]
    public void ResolverHonoursPersistedSelection()
    {
        using TranscriptionService service = new();
        FakeProvider provider = new("Models",
            new TranscriptionModel("default", "Default", "Default", 1, true),
            new TranscriptionModel("selected", "Selected", "Selected", 1));
        provider.Installed.Add("default");
        provider.Installed.Add("selected");
        using IDisposable registration = service.Register(provider);

        string? result = TranscriptionModelResolver.ResolveInstalledModel(service,
            new FakeSelection("Models/selected"));

        Assert.Equal("Models/selected", result);
    }

    [Fact]
    public void ProviderCanPublishSeveralModels()
    {
        using TranscriptionService service = new();
        using IDisposable registration = service.Register(new FakeProvider("Provider",
            new TranscriptionModel("one", "One", "First", 1, true),
            new TranscriptionModel("two", "Two", "Second", 1)));

        Assert.Equal(["Provider/one", "Provider/two"], service.Models.Select(model => model.Id));
        Assert.All(service.Models, model => Assert.Equal("Provider", model.ProviderId));
    }

    private sealed class FakeProvider(string id,
        params TranscriptionModel[] models) :
        ITranscriptionProvider
    {
        public event EventHandler? StateChanged;

        public string Id { get; } = id;

        public string DisplayName => Id;

        public IReadOnlyList<TranscriptionModel> Models { get; } = models;

        public string DefaultModelId => Models.First(model => model.IsRecommended).Id;

        public HashSet<string> Installed { get; } = [with(StringComparer.OrdinalIgnoreCase), .. models.Select(model => model.Id)];

        public int DecoderCount { get; private set; }

        public bool IsInstalled(string modelId) => Installed.Contains(modelId);

        public Task<TranscriptionModelState> GetStateAsync(string modelId,
            CancellationToken cancellationToken = default) => Task.FromResult(IsInstalled(modelId)
                ? TranscriptionModelState.Installed
                : TranscriptionModelState.NotInstalled);

        public Task InstallAsync(string modelId,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            _ = Installed.Add(modelId);
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string modelId,
            CancellationToken cancellationToken = default)
        {
            _ = Installed.Remove(modelId);
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public TranscriptionModelDownload? GetDownload(string modelId) => null;

        public bool CancelInstall(string modelId) => false;

        public TranscriptionAudioFormat GetAudioFormat(string modelId) => TranscriptionAudioFormat.Speech;

        public Task<ITranscriptionDecoder> CreateDecoderAsync(string modelId,
            string language = "auto",
            CancellationToken cancellationToken = default)
        {
            DecoderCount++;
            return Task.FromResult<ITranscriptionDecoder>(new FakeDecoder());
        }
    }

    private sealed class FakeSelection(string? modelId) :
        ITranscriptionModelSelection
    {
        public event EventHandler? SelectionChanged
        {
            add { }
            remove { }
        }

        public string? SelectedModelId { get; } = modelId;

        public Task SelectAsync(string modelId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeDecoder :
        ITranscriptionDecoder
    {
        public Task AppendAsync(ReadOnlyMemory<byte> audio,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public async IAsyncEnumerable<TranscriptionResult> GetResultsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task CompleteAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
