namespace Glance.Transcription;

public sealed record TranscriptionAudioFormat(int SampleRate,
    int Channels = 1,
    int BitsPerSample = 16)
{
    public static TranscriptionAudioFormat Speech { get; } = new(16000);
}
