namespace Glance.VoiceNotes;

public sealed class VoiceLevelsChangedEventArgs(IReadOnlyList<double> levels) :
    EventArgs
{
    public IReadOnlyList<double> Levels { get; } = levels;
}

public sealed class VoiceRecordingCompletedEventArgs(
    VoiceNote? recording,
    Exception? error = null) :
    EventArgs
{
    public VoiceNote? Recording { get; } = recording;

    public Exception? Error { get; } = error;
}
