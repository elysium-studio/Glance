namespace Glance.ScreenCapture;

public sealed class ScreenCaptureSettings
{
    public bool CopyToClipboardAutomatically { get; set; }

    public double RecentCaptureLimit { get; set; } = 6;
}
