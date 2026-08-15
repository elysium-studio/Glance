namespace Glance.Transcription;

public sealed record TranscriptionSessionOptions(string ModelId,
    string AudioInputSourceId,
    string Language = "auto");
