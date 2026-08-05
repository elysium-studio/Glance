namespace Glance.ScreenRecorder;

public sealed class ScreenRecorderSettings
{
    public double CountdownSeconds { get; set; } = 3;

    public bool IncludeCursor { get; set; } = true;

    public double RecentRecordingLimit { get; set; } = 6;
}
