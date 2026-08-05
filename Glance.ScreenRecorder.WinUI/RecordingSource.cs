namespace Glance.ScreenRecorder.WinUI;

internal sealed record RecordingSource(ScreenRecordingMode Mode,
    NativeRectangle Bounds,
    nint WindowHandle,
    nint MonitorHandle);

internal readonly record struct NativeRectangle(int Left, int Top, int Right, int Bottom)
{
    public int Width => Math.Max(0, Right - Left);

    public int Height => Math.Max(0, Bottom - Top);
}

internal sealed record RecordingSelectionCandidate(NativeRectangle Bounds,
    nint WindowHandle,
    nint MonitorHandle);
