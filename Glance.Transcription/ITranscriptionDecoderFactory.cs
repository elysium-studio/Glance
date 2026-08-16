namespace Glance.Transcription;

public interface ITranscriptionDecoderFactory
{
    TranscriptionAudioFormat GetAudioFormat(string modelId);

    Task<ITranscriptionDecoder> CreateDecoderAsync(string modelId,
        string language = "auto",
        CancellationToken cancellationToken = default);
}
