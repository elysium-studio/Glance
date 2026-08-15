namespace Glance.Transcription;

public interface ITranscriptionSession :
    IAsyncDisposable
{
    IAsyncEnumerable<TranscriptionResult> GetResultsAsync(CancellationToken cancellationToken = default);

    Task PauseAsync(CancellationToken cancellationToken = default);

    Task ResumeAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
