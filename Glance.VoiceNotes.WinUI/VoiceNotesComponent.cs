using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Diagnostics;

namespace Glance.VoiceNotes.WinUI;

public sealed class VoiceNotesComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private static readonly double[] SilentLevels = new double[12];

    private readonly DispatcherQueue dispatcherQueue;
    private readonly ITextLocalizer localizer;
    private readonly IVoiceRecordingService recordingService;
    private readonly DispatcherQueueTimer timer;
    private readonly VoiceNotesViewModel viewModel;
    private long recordingStartedTimestamp;

    public VoiceNotesComponent(
        VoiceNotesViewModel viewModel,
        IVoiceRecordingService recordingService,
        ModuleResourceTextLocalizer<VoiceNotesModule> localizer)
    {
        this.viewModel = viewModel;
        this.recordingService = recordingService;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        VoiceNotesCompactView compactView = new(viewModel);
        VoiceNotesExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        timer = dispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(100);
        timer.IsRepeating = true;
        timer.Tick += HandleTick;

        viewModel.RecordingToggleRequested += HandleRecordingToggleRequested;
        viewModel.OpenRequested += HandleOpenRequested;
        viewModel.DeleteRequested += HandleDeleteRequested;
        recordingService.LevelsChanged += HandleLevelsChanged;
        recordingService.RecordingCompleted += HandleRecordingCompleted;
        viewModel.SetRecordings(recordingService.GetRecentRecordings(3));
    }

    public string Id => "VoiceNotes";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 90;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= HandleTick;
        viewModel.RecordingToggleRequested -= HandleRecordingToggleRequested;
        viewModel.OpenRequested -= HandleOpenRequested;
        viewModel.DeleteRequested -= HandleDeleteRequested;
        recordingService.LevelsChanged -= HandleLevelsChanged;
        recordingService.RecordingCompleted -= HandleRecordingCompleted;

        if (recordingService.IsRecording)
        {
            recordingService.StopRecording();
        }
    }

    private void HandleRecordingToggleRequested(object? sender, EventArgs args)
    {
        if (recordingService.IsRecording)
        {
            timer.Stop();
            recordingService.StopRecording();
            return;
        }

        if (!recordingService.StartRecording())
        {
            viewModel.ShowRecordingError();
            return;
        }

        recordingStartedTimestamp = Stopwatch.GetTimestamp();
        viewModel.BeginRecording();
        timer.Start();
    }

    private void HandleOpenRequested(object? sender, VoiceNote recording) =>
        recordingService.TryOpen(recording);

    private void HandleDeleteRequested(object? sender, VoiceNote recording)
    {
        if (recordingService.TryDelete(recording))
        {
            viewModel.RemoveRecording(recording);
        }
    }

    private void HandleLevelsChanged(
        object? sender,
        VoiceLevelsChangedEventArgs args) =>
        dispatcherQueue.TryEnqueue(() => viewModel.UpdateAudioLevels(args.Levels));

    private void HandleRecordingCompleted(
        object? sender,
        VoiceRecordingCompletedEventArgs args) =>
        dispatcherQueue.TryEnqueue(() =>
        {
            timer.Stop();
            recordingStartedTimestamp = 0;
            viewModel.UpdateAudioLevels(SilentLevels);

            if (args.Error is not null)
            {
                viewModel.ShowRecordingError();
                return;
            }

            viewModel.FinishRecording(args.Recording);
        });

    private void HandleTick(DispatcherQueueTimer sender, object args)
    {
        if (recordingStartedTimestamp != 0)
        {
            viewModel.UpdateElapsed(
                Stopwatch.GetElapsedTime(recordingStartedTimestamp));
        }
    }
}
