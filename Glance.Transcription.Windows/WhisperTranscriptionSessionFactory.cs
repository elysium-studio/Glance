using Glance.Transcription;
using NAudio.CoreAudioApi;
using Whisper.net;

namespace Glance.Transcription.Windows;

public sealed class WhisperTranscriptionSessionFactory(ITranscriptionModelCatalog modelCatalog) :
    ITranscriptionSessionFactory
{
    private readonly ITranscriptionModelCatalog modelCatalog = modelCatalog;

    public async Task<ITranscriptionSession> CreateAsync(TranscriptionSessionOptions options,
        CancellationToken cancellationToken = default)
    {
        if (await modelCatalog.GetStateAsync(options.ModelId, cancellationToken) != TranscriptionModelState.Installed)
        {
            throw new InvalidOperationException("The selected transcription model is not installed");
        }

        using MMDeviceEnumerator enumerator = new();
        MMDevice device = enumerator.GetDevice(options.AudioInputSourceId);
        WhisperFactory factory = WhisperFactory.FromPath(modelCatalog.GetModelPath(options.ModelId));
        WhisperProcessor processor = factory.CreateBuilder()
            .WithLanguage(string.IsNullOrWhiteSpace(options.Language) ? "auto" : options.Language)
            .Build();

        try
        {
            WhisperTranscriptionSession session = new(factory, processor, device);
            await session.StartAsync(cancellationToken);
            return session;
        }
        catch
        {
            processor.Dispose();
            factory.Dispose();
            device.Dispose();
            throw;
        }
    }
}
