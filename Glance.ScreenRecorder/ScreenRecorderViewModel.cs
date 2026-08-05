using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;

namespace Glance.ScreenRecorder;

public sealed partial class ScreenRecorderViewModel(ITextLocalizer localizer, ScreenRecorderSettings? settings = null) :
    ObservableObject
{
    private readonly ITextLocalizer localizer = localizer;
    private int recentRecordingLimit = GetRecentRecordingLimit(settings ?? new ScreenRecorderSettings());

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyPropertyChangedFor(nameof(IsRecording))]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    private ScreenRecordingState state;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    private string statusText = localizer.GetText("ReadyToRecord");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    private string elapsedText = "00:00";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    private bool isPaused;

    [ObservableProperty]
    private bool hasRecordings;

    [ObservableProperty]
    private ScreenRecordingItemViewModel? selectedRecording;

    public string Title => localizer.GetText("ModuleTitle");

    public string RecordRegionText => localizer.GetText("RecordRegion");

    public string RecordWindowText => localizer.GetText("RecordWindow");

    public string RecordDisplayText => localizer.GetText("RecordDisplay");

    public bool IsBusy => State is ScreenRecordingState.Selecting or ScreenRecordingState.CountingDown or ScreenRecordingState.Saving;

    public bool IsRecording => State == ScreenRecordingState.Recording;

    public string CompactStatusText => State switch
    {
        ScreenRecordingState.Recording when IsPaused => localizer.GetText("RecordingPaused"),
        ScreenRecordingState.Recording => ElapsedText,
        ScreenRecordingState.CountingDown => StatusText,
        _ => StatusText
    };

    public ObservableCollection<ScreenRecordingItemViewModel> Recordings { get; } = [];

    public event EventHandler<ScreenRecordingMode>? RecordingRequested;

    public event EventHandler? StopRequested;

    public event EventHandler<ScreenRecording>? OpenRequested;

    public event EventHandler<ScreenRecording>? RevealRequested;

    public event EventHandler<ScreenRecording>? DeleteRequested;

    public void RecordRegion() => RequestRecording(ScreenRecordingMode.Region);

    public void RecordWindow() => RequestRecording(ScreenRecordingMode.Window);

    public void RecordDisplay() => RequestRecording(ScreenRecordingMode.Display);

    public void ToggleRecording()
    {
        if (IsRecording)
        {
            StopRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetRecordings(IEnumerable<ScreenRecording> recordings)
    {
        Recordings.Clear();

        foreach (ScreenRecording recording in recordings)
        {
            Recordings.Add(CreateItem(recording));
        }

        TrimRecordings();
        UpdateSelection();
    }

    public void ApplyState(ScreenRecordingStateChangedEventArgs args)
    {
        State = args.State;
        IsPaused = args.IsPaused;
        ElapsedText = FormatElapsed(args.Elapsed);
        StatusText = args.State switch
        {
            ScreenRecordingState.Selecting => localizer.GetText("SelectingRecording"),
            ScreenRecordingState.CountingDown => localizer.GetText("Countdown", args.Countdown),
            ScreenRecordingState.Recording when args.IsPaused => localizer.GetText("RecordingPaused"),
            ScreenRecordingState.Recording => localizer.GetText("RecordingStatus"),
            ScreenRecordingState.Saving => localizer.GetText("SavingRecording"),
            ScreenRecordingState.Completed => localizer.GetText("RecordingSaved"),
            ScreenRecordingState.Failed => localizer.GetText("RecordingFailed"),
            _ => localizer.GetText("ReadyToRecord")
        };

        if (args.Recording is not null)
        {
            ScreenRecordingItemViewModel item = CreateItem(args.Recording);
            Recordings.Insert(0, item);
            TrimRecordings();
            HasRecordings = true;
            SelectedRecording = item;
        }
    }

    public void ApplySettings(ScreenRecorderSettings settings)
    {
        recentRecordingLimit = GetRecentRecordingLimit(settings);
        TrimRecordings();
        UpdateSelection();
    }

    public void Remove(ScreenRecording recording)
    {
        ScreenRecordingItemViewModel? item = Recordings.FirstOrDefault(value =>
            string.Equals(value.Recording.FilePath, recording.FilePath, StringComparison.OrdinalIgnoreCase));

        if (item is not null)
        {
            _ = Recordings.Remove(item);
        }

        UpdateSelection();
    }

    private void RequestRecording(ScreenRecordingMode mode)
    {
        if (State is not ScreenRecordingState.Idle and not ScreenRecordingState.Completed and not ScreenRecordingState.Failed)
        {
            return;
        }

        State = ScreenRecordingState.Selecting;
        StatusText = localizer.GetText("SelectingRecording");
        RecordingRequested?.Invoke(this, mode);
    }

    private void TrimRecordings()
    {
        while (Recordings.Count > recentRecordingLimit)
        {
            Recordings.RemoveAt(Recordings.Count - 1);
        }
    }

    private void UpdateSelection()
    {
        HasRecordings = Recordings.Count > 0;
        SelectedRecording = Recordings.FirstOrDefault();
    }

    private static int GetRecentRecordingLimit(ScreenRecorderSettings settings) => (int)Math.Clamp(settings.RecentRecordingLimit, 1, 12);

    private static string FormatElapsed(TimeSpan elapsed) => elapsed.TotalHours >= 1
        ? $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
        : $"{elapsed.Minutes:00}:{elapsed.Seconds:00}";

    private ScreenRecordingItemViewModel CreateItem(ScreenRecording recording) => new(recording,
            localizer.GetText("RecordingItemDetail", FormatElapsed(recording.Duration), recording.Width, recording.Height),
            value => OpenRequested?.Invoke(this, value),
            value => RevealRequested?.Invoke(this, value),
            value => DeleteRequested?.Invoke(this, value));
}
