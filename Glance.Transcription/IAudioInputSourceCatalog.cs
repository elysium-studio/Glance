namespace Glance.Transcription;

public interface IAudioInputSourceCatalog
{
    Task<IReadOnlyList<AudioInputSource>> GetSourcesAsync(CancellationToken cancellationToken = default);
}
