namespace Glance.PrivacyControls.WinUI;

public sealed class WindowsCameraUsageService :
    ICameraUsageService
{
    public bool IsInUse() =>
        WindowsCapabilityUsageReader.IsInUse("webcam");
}
