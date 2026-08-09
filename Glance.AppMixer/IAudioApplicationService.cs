namespace Glance.AppMixer;

public interface IAudioApplicationService
{
    IReadOnlyList<AudioApplicationSession> GetApplications();

    bool TrySetVolume(string applicationId,
        int volumePercent);

    bool TrySetMuted(string applicationId,
        bool isMuted);
}
