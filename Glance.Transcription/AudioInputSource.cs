namespace Glance.Transcription;

public sealed record AudioInputSource(string Id,
    string DisplayName,
    bool IsDefault = false);
