using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;

namespace Glance.VoiceNotes;

public sealed partial class VoiceNotesViewModel :
    ObservableObject
{
    private readonly ITextLocalizer localizer;
    private int recentRecordingLimit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleGlyph))]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    [NotifyPropertyChangedFor(nameof(ExpandedStatusText))]
    private bool isRecording;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    [NotifyPropertyChangedFor(nameof(ExpandedStatusText))]
    private string elapsedText = "00:00";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpandedStatusText))]
    private string statusText;

    [ObservableProperty]
    private bool hasRecordings;

    [ObservableProperty]
    private VoiceNoteItemViewModel? selectedRecording;

    public VoiceNotesViewModel(ITextLocalizer localizer, VoiceNotesSettings? settings = null)
    {
        this.localizer = localizer;
        recentRecordingLimit = GetRecentRecordingLimit(settings ?? new VoiceNotesSettings());
        statusText = localizer.GetText("ReadyToRecord");
    }

    public ObservableCollection<VoiceNoteItemViewModel> Recordings { get; } = [];

    public string ToggleGlyph => IsRecording ? "\uF78A" : "\uE720";

    public string CompactStatusText => IsRecording
        ? ElapsedText
        : localizer.GetText("ModuleDisplayName");

    public string ExpandedStatusText => IsRecording
        ? ElapsedText
        : StatusText;

    public event EventHandler? RecordingToggleRequested;

    public event EventHandler<VoiceNote>? OpenRequested;

    public event EventHandler<VoiceNote>? DeleteRequested;

    public event EventHandler<VoiceLevelsChangedEventArgs>? AudioLevelsChanged;

    public void ToggleRecording() =>
        RecordingToggleRequested?.Invoke(this, EventArgs.Empty);

    public void Open(VoiceNote recording) =>
        OpenRequested?.Invoke(this, recording);

    public void Delete(VoiceNote recording) =>
        DeleteRequested?.Invoke(this, recording);

    public void SetRecordings(IEnumerable<VoiceNote> recordings)
    {
        Recordings.Clear();

        foreach (VoiceNote recording in recordings)
        {
            Recordings.Add(CreateItem(recording));
        }

        TrimRecordings();

        HasRecordings = Recordings.Count > 0;
        SelectedRecording = Recordings.FirstOrDefault();
    }

    public void BeginRecording()
    {
        ElapsedText = "00:00";
        StatusText = localizer.GetText("RecordingStatus");
        IsRecording = true;
    }

    public void UpdateElapsed(TimeSpan elapsed) =>
        ElapsedText = FormatElapsed(elapsed);

    public void FinishRecording(VoiceNote? recording)
    {
        IsRecording = false;
        ElapsedText = "00:00";
        StatusText = localizer.GetText("ReadyToRecord");

        if (recording is null)
        {
            return;
        }

        VoiceNoteItemViewModel? existing = Recordings.FirstOrDefault(item =>
            string.Equals(item.Recording.FilePath, recording.FilePath, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            Recordings.Remove(existing);
        }

        Recordings.Insert(0, CreateItem(recording));

        TrimRecordings();

        HasRecordings = true;
        SelectedRecording = Recordings[0];
    }

    public void RemoveRecording(VoiceNote recording)
    {
        VoiceNoteItemViewModel? item = Recordings.FirstOrDefault(value =>
            string.Equals(value.Recording.FilePath, recording.FilePath, StringComparison.OrdinalIgnoreCase));

        if (item is not null)
        {
            bool wasSelected = ReferenceEquals(SelectedRecording, item);
            Recordings.Remove(item);

            if (wasSelected)
            {
                SelectedRecording = Recordings.FirstOrDefault();
            }
        }

        HasRecordings = Recordings.Count > 0;

        if (!HasRecordings)
        {
            SelectedRecording = null;
        }
    }

    public void ShowRecordingError()
    {
        IsRecording = false;
        ElapsedText = "00:00";
        StatusText = localizer.GetText("RecordingUnavailable");
    }

    public void UpdateAudioLevels(IReadOnlyList<double> levels) =>
        AudioLevelsChanged?.Invoke(
            this,
            new VoiceLevelsChangedEventArgs([.. levels]));

    public void ApplySettings(VoiceNotesSettings settings)
    {
        recentRecordingLimit = GetRecentRecordingLimit(settings);
        TrimRecordings();
        HasRecordings = Recordings.Count > 0;
        SelectedRecording ??= Recordings.FirstOrDefault();
    }

    private void TrimRecordings()
    {
        while (Recordings.Count > recentRecordingLimit)
        {
            Recordings.RemoveAt(Recordings.Count - 1);
        }
    }

    private static int GetRecentRecordingLimit(VoiceNotesSettings settings) =>
        (int)Math.Clamp(settings.RecentRecordingLimit, 1, 10);

    private static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.TotalHours >= 1
            ? $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
            : $"{elapsed.Minutes:00}:{elapsed.Seconds:00}";

    private VoiceNoteItemViewModel CreateItem(VoiceNote recording) =>
        new(recording, Open, Delete);
}
