namespace Glance.ScreenRecorder;

public sealed class ScreenRecordingStateChangedEventArgs(ScreenRecordingState state,
    TimeSpan elapsed,
    int countdown,
    ScreenRecording? recording = null,
    bool isPaused = false) :
    EventArgs
{
    public ScreenRecordingState State { get; } = state;

    public TimeSpan Elapsed { get; } = elapsed;

    public int Countdown { get; } = countdown;

    public ScreenRecording? Recording { get; } = recording;

    public bool IsPaused { get; } = isPaused;
}
