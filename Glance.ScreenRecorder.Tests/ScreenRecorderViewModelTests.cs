using Glance.Application.Abstractions;

namespace Glance.ScreenRecorder.Tests;

public sealed class ScreenRecorderViewModelTests
{
    [Theory]
    [InlineData(ScreenRecordingMode.Region)]
    [InlineData(ScreenRecordingMode.Window)]
    [InlineData(ScreenRecordingMode.Display)]
    public void RecordingFunctions_RequestExpectedMode(ScreenRecordingMode expectedMode)
    {
        ScreenRecorderViewModel viewModel = CreateViewModel();
        ScreenRecordingMode? requestedMode = null;
        viewModel.RecordingRequested += (_, mode) => requestedMode = mode;

        InvokeRecording(viewModel, expectedMode);

        Assert.Equal(ScreenRecordingState.Selecting, viewModel.State);
        Assert.Equal(expectedMode, requestedMode);
    }

    [Fact]
    public void ApplyState_FormatsElapsedRecordingTime()
    {
        ScreenRecorderViewModel viewModel = CreateViewModel();

        viewModel.ApplyState(new ScreenRecordingStateChangedEventArgs(ScreenRecordingState.Recording, TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(9), 0, null));

        Assert.True(viewModel.IsRecording);
        Assert.Equal("02:09", viewModel.ElapsedText);
        Assert.Equal("02:09", viewModel.CompactStatusText);
    }

    [Fact]
    public void PausedRecording_ShowsPausedStateAndRemainsActive()
    {
        ScreenRecorderViewModel viewModel = CreateViewModel();

        viewModel.ApplyState(new ScreenRecordingStateChangedEventArgs(ScreenRecordingState.Recording,
            TimeSpan.FromSeconds(12),
            0,
            isPaused: true));

        Assert.True(viewModel.IsRecording);
        Assert.True(viewModel.IsPaused);
        Assert.Equal("Recording paused", viewModel.CompactStatusText);
    }

    [Fact]
    public void CompletedRecording_IsAddedAndSelected()
    {
        ScreenRecorderViewModel viewModel = CreateViewModel();
        ScreenRecording recording = new("C:\\Recordings\\demo.mp4", DateTimeOffset.Now, TimeSpan.FromSeconds(18), 1920, 1080, ScreenRecordingMode.Window);

        viewModel.ApplyState(new ScreenRecordingStateChangedEventArgs(ScreenRecordingState.Completed, recording.Duration, 0, recording));

        Assert.True(viewModel.HasRecordings);
        _ = Assert.Single(viewModel.Recordings);
        Assert.Equal(recording, viewModel.SelectedRecording?.Recording);
    }

    [Fact]
    public void RecentRecordingLimit_ComesFromModuleSettings()
    {
        ScreenRecorderViewModel viewModel = new(new FakeLocalizer(), new ScreenRecorderSettings { RecentRecordingLimit = 2 });

        viewModel.SetRecordings(Enumerable.Range(0, 4).Select(index =>
            new ScreenRecording($"C:\\Recordings\\recording-{index}.mp4", DateTimeOffset.Now, TimeSpan.Zero, 1280, 720, ScreenRecordingMode.Display)));

        Assert.Equal(2, viewModel.Recordings.Count);
    }

    private static ScreenRecorderViewModel CreateViewModel() => new(new FakeLocalizer());

    private static void InvokeRecording(ScreenRecorderViewModel viewModel, ScreenRecordingMode mode)
    {
        switch (mode)
        {
            case ScreenRecordingMode.Region:
                viewModel.RecordRegion();
                break;
            case ScreenRecordingMode.Window:
                viewModel.RecordWindow();
                break;
            case ScreenRecordingMode.Display:
                viewModel.RecordDisplay();
                break;
        }
    }

    private sealed class FakeLocalizer :
        ITextLocalizer
    {
        private static readonly IReadOnlyDictionary<string, string> Values = new Dictionary<string, string>
        {
            ["ModuleTitle"] = "Screen recorder",
            ["ReadyToRecord"] = "Ready to record",
            ["SelectingRecording"] = "Select what to record",
            ["Countdown"] = "Recording in {0}",
            ["RecordingStatus"] = "Recording",
            ["RecordingPaused"] = "Recording paused",
            ["SavingRecording"] = "Saving recording",
            ["RecordingSaved"] = "Recording saved",
            ["RecordingFailed"] = "Recording unavailable",
            ["RecordingItemDetail"] = "{0} · {1} × {2}",
            ["RecordRegion"] = "Region",
            ["RecordWindow"] = "Window",
            ["RecordDisplay"] = "Full screen"
        };

        public string GetText(string key, params object[] arguments) => string.Format(Values[key], arguments);
    }
}
