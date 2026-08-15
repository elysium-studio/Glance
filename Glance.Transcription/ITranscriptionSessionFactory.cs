namespace Glance.Transcription;

public interface ITranscriptionSessionFactory
{
    Task<ITranscriptionSession> CreateAsync(TranscriptionSessionOptions options,
        CancellationToken cancellationToken = default);
}
