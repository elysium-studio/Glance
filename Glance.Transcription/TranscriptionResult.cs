namespace Glance.Transcription;

public sealed record TranscriptionResult(string Text,
    bool IsFinal,
    TimeSpan Start,
    TimeSpan End);
