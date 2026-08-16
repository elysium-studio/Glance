namespace Glance.Transcription;

public interface ITranscriptionProviderRegistry
{
    IDisposable Register(ITranscriptionProvider provider);
}
