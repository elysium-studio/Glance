using Glance.Application.Abstractions;

namespace Glance.VoiceNotes.Tests;

public sealed class VoiceNotesViewModelTests
{
    [Fact]
    public void Constructor_ShowsReadyState()
    {
        VoiceNotesViewModel viewModel = new(new FakeLocalizer());

        Assert.False(viewModel.IsRecording);
        Assert.Equal("Ready to record", viewModel.StatusText);
        Assert.Equal("Voice notes", viewModel.CompactStatusText);
        Assert.Equal("Ready to record", viewModel.ExpandedStatusText);
        Assert.Equal("\uE720", viewModel.ToggleGlyph);
    }

    [Fact]
    public void BeginRecording_UsesElapsedTimeAsCompactStatus()
    {
        VoiceNotesViewModel viewModel = new(new FakeLocalizer());

        viewModel.BeginRecording();
        viewModel.UpdateElapsed(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(7));

        Assert.True(viewModel.IsRecording);
        Assert.Equal("02:07", viewModel.ElapsedText);
        Assert.Equal("02:07", viewModel.CompactStatusText);
        Assert.Equal("\uF78A", viewModel.ToggleGlyph);
    }

    [Fact]
    public void FinishRecording_PutsNewestFirstAndLimitsHistory()
    {
        VoiceNotesViewModel viewModel = new(new FakeLocalizer());
        VoiceNote[] existing = Enumerable.Range(1, 3)
            .Select(index => CreateNote($"note-{index}.wav", index))
            .ToArray();
        VoiceNote latest = CreateNote("latest.wav", 10);

        viewModel.SetRecordings(existing);
        viewModel.BeginRecording();
        viewModel.FinishRecording(latest);

        Assert.False(viewModel.IsRecording);
        Assert.True(viewModel.HasRecordings);
        Assert.Equal(3, viewModel.Recordings.Count);
        Assert.Same(latest, viewModel.Recordings[0].Recording);
        Assert.Same(viewModel.Recordings[0], viewModel.SelectedRecording);
        Assert.DoesNotContain(
            viewModel.Recordings,
            item => ReferenceEquals(existing[^1], item.Recording));
    }

    [Fact]
    public void RemoveRecording_UpdatesEmptyState()
    {
        VoiceNotesViewModel viewModel = new(new FakeLocalizer());
        VoiceNote recording = CreateNote("note.wav", 1);
        viewModel.SetRecordings([recording]);

        viewModel.RemoveRecording(recording);

        Assert.Empty(viewModel.Recordings);
        Assert.False(viewModel.HasRecordings);
        Assert.Null(viewModel.SelectedRecording);
    }

    [Fact]
    public void Actions_RaiseFunctionEventsWithRecording()
    {
        VoiceNotesViewModel viewModel = new(new FakeLocalizer());
        VoiceNote recording = CreateNote("note.wav", 1);
        int toggles = 0;
        VoiceNote? opened = null;
        VoiceNote? deleted = null;
        viewModel.RecordingToggleRequested += (_, _) => toggles++;
        viewModel.OpenRequested += (_, value) => opened = value;
        viewModel.DeleteRequested += (_, value) => deleted = value;

        viewModel.ToggleRecording();
        viewModel.Open(recording);
        viewModel.Delete(recording);

        Assert.Equal(1, toggles);
        Assert.Same(recording, opened);
        Assert.Same(recording, deleted);
    }

    [Fact]
    public void VoiceNote_FormatsShortAndLongDurations()
    {
        VoiceNote shortNote = new(
            "short.wav",
            DateTimeOffset.Now,
            new TimeSpan(0, 0, 3, 5));
        VoiceNote longNote = new(
            "long.wav",
            DateTimeOffset.Now,
            new TimeSpan(0, 2, 3, 5));

        Assert.Equal("03:05", shortNote.DurationText);
        Assert.Equal("02:03:05", longNote.DurationText);
    }

    [Fact]
    public void SetRecordings_SelectsNewestRecordingAndExposesFullFileName()
    {
        VoiceNotesViewModel viewModel = new(new FakeLocalizer());
        VoiceNote recording = CreateNote("voice-note.wav", 1);

        viewModel.SetRecordings([recording]);

        Assert.Same(viewModel.Recordings[0], viewModel.SelectedRecording);
        Assert.Equal("voice-note.wav", viewModel.Recordings[0].FileName);
    }

    private static VoiceNote CreateNote(string name, int minute) =>
        new(name, DateTimeOffset.Now.AddMinutes(minute), TimeSpan.FromSeconds(minute));

    private sealed class FakeLocalizer :
        ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) => key switch
        {
            "ReadyToRecord" => "Ready to record",
            "RecordingStatus" => "Recording",
            "RecordingUnavailable" => "Microphone unavailable",
            "ModuleDisplayName" => "Voice notes",
            _ => key
        };
    }
}
