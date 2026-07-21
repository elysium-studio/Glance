namespace Glance.VoiceNotes;

public interface IVoiceRecordingService :
    IDisposable
{
    event EventHandler<VoiceLevelsChangedEventArgs>? LevelsChanged;

    event EventHandler<VoiceRecordingCompletedEventArgs>? RecordingCompleted;

    bool IsRecording { get; }

    IReadOnlyList<VoiceNote> GetRecentRecordings(int maximumCount);

    bool StartRecording();

    void StopRecording();

    bool TryOpen(VoiceNote recording);

    bool TryDelete(VoiceNote recording);
}
