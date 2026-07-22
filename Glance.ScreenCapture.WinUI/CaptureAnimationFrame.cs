namespace Glance.ScreenCapture.WinUI;

internal sealed record CaptureAnimationFrame(
    DesktopCaptureBitmap Bitmap,
    NativeRectangle DesktopBounds);
