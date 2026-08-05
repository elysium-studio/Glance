namespace Glance.ScreenRecorder;

public sealed class ScreenRecordingItemViewModel(ScreenRecording recording,
    string detail,
    Action<ScreenRecording> open,
    Action<ScreenRecording> reveal,
    Action<ScreenRecording> delete)
{
    public ScreenRecording Recording { get; } = recording;

    public string FileName => Recording.FileName;

    public string Detail { get; } = detail;

    public void Open() => open(Recording);

    public void Reveal() => reveal(Recording);

    public void Delete() => delete(Recording);
}
