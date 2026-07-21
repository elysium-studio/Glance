namespace Glance.ScreenCapture;

public sealed record ScreenCaptureItem(
    string FilePath,
    string FileName,
    DateTimeOffset CapturedAt,
    int Width,
    int Height,
    ScreenCaptureMode Mode);
