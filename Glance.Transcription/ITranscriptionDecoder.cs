namespace Glance.Transcription;

public interface ITranscriptionDecoder :
    IAsyncDisposable
{
    Task AppendAsync(ReadOnlyMemory<byte> audio,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<TranscriptionResult> GetResultsAsync(CancellationToken cancellationToken = default);

    Task CompleteAsync(CancellationToken cancellationToken = default);
}
