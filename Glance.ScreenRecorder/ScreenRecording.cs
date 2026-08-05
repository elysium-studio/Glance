namespace Glance.ScreenRecorder;

public sealed record ScreenRecording(string FilePath,
    DateTimeOffset CreatedAt,
    TimeSpan Duration,
    int Width,
    int Height,
    ScreenRecordingMode Mode)
{
    public string FileName => Path.GetFileName(FilePath);
}
