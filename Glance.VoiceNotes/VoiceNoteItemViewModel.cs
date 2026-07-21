namespace Glance.VoiceNotes;

public sealed class VoiceNoteItemViewModel(
    VoiceNote recording,
    Action<VoiceNote> open,
    Action<VoiceNote> delete)
{
    public VoiceNote Recording { get; } = recording;

    public string DisplayName => Recording.DisplayName;

    public string DurationText => Recording.DurationText;

    public string CreatedText => Recording.CreatedText;

    public void Open() => open(Recording);

    public void Delete() => delete(Recording);
}
