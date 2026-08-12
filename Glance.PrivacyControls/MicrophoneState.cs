namespace Glance.PrivacyControls;

public sealed record MicrophoneState(string DeviceName,
    bool IsAvailable,
    bool IsMuted,
    bool IsInUse)
{
    public static MicrophoneState Unavailable { get; } = new(string.Empty, false, false, false);
}
