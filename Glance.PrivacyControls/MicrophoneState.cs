namespace Glance.PrivacyControls;

public sealed record MicrophoneState(
    string DeviceName,
    bool IsAvailable,
    bool IsMuted,
    double PeakLevel)
{
    public static MicrophoneState Unavailable { get; } = new(string.Empty, false, false, 0);
}
