namespace Glance.VoiceNotes;

public sealed record VoiceNote(
    string FilePath,
    DateTimeOffset CreatedAt,
    TimeSpan Duration)
{
    public string DisplayName => Path.GetFileNameWithoutExtension(FilePath);

    public string DurationText => Duration.TotalHours >= 1
        ? $"{(int)Duration.TotalHours:00}:{Duration.Minutes:00}:{Duration.Seconds:00}"
        : $"{Duration.Minutes:00}:{Duration.Seconds:00}";

    public string CreatedText => CreatedAt.LocalDateTime.ToString("g");
}
