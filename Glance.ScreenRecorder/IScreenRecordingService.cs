namespace Glance.ScreenRecorder;

public interface IScreenRecordingService
{
    event EventHandler<ScreenRecordingStateChangedEventArgs>? StateChanged;

    ScreenRecordingState State { get; }

    bool IsPaused { get; }

    bool IsCursorCaptureEnabled { get; }

    IReadOnlyList<ScreenRecording> GetRecentRecordings(int maximumCount);

    Task<bool> StartAsync(ScreenRecordingMode mode,
        int countdownSeconds,
        bool includeCursor,
        string? windowName = null,
        CancellationToken cancellationToken = default);

    Task<ScreenRecording?> StopAsync(CancellationToken cancellationToken = default);

    Task<bool> SetPausedAsync(bool paused, CancellationToken cancellationToken = default);

    bool SetCursorCaptureEnabled(bool enabled);

    int CountMatchingWindows(string windowName);

    bool TryOpen(ScreenRecording recording);

    bool TryReveal(ScreenRecording recording);

    bool TryDelete(ScreenRecording recording);
}
