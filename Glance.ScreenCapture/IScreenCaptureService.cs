namespace Glance.ScreenCapture;

public interface IScreenCaptureService
{
    Task<ScreenCaptureItem?> CaptureAsync(ScreenCaptureMode mode);

    IReadOnlyList<ScreenCaptureItem> GetRecentCaptures(int maximumCount);

    bool TryOpen(ScreenCaptureItem capture);

    bool TryReveal(ScreenCaptureItem capture);

    Task<bool> TryCopyAsync(ScreenCaptureItem capture);

    bool TryDelete(ScreenCaptureItem capture);
}
