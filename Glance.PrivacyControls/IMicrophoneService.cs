namespace Glance.PrivacyControls;

public interface IMicrophoneService
{
    MicrophoneState GetState();

    bool TrySetMuted(bool isMuted);
}
