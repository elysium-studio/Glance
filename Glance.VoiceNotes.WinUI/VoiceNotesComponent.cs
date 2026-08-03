using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.VoiceNotes.WinUI;

public sealed partial class VoiceNotesComponent :
    IGlanceComponent,
    IGlanceActionProvider,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private static readonly double[] SilentLevels = new double[12];

    private readonly DispatcherQueue dispatcherQueue;
    private readonly ITextLocalizer localizer;
    private readonly IVoiceRecordingService recordingService;
    private readonly DispatcherQueueTimer timer;
    private readonly VoiceNotesViewModel viewModel;
    private readonly GlanceModuleOptions<VoiceNotesSettings> options;
    private readonly double[] waveformHistory = new double[42];
    private long recordingStartedTimestamp;

    public VoiceNotesComponent(VoiceNotesViewModel viewModel,
        IVoiceRecordingService recordingService,
        GlanceModuleOptions<VoiceNotesSettings> options,
        ModuleResourceTextLocalizer<VoiceNotesModule> localizer)
    {
        this.viewModel = viewModel;
        this.recordingService = recordingService;
        this.options = options;
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
        options.Changed += HandleOptionsChanged;
        viewModel.SetRecordings(recordingService.GetRecentRecordings(RecentRecordingLimit));
    }

    public string Id => "VoiceNotes";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public string SettingsCategory => GlanceModuleCategories.MediaAndCapture;

    public int Order => 90;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public IReadOnlyList<GlanceActionDescriptor> GetActions() =>
    [
        new GlanceActionDescriptor("VoiceNotes.Start", Id, "Start voice note", "Record microphone audio as a saved voice note. This does not transcribe speech into text.")
        {
            SemanticTags = ["voice note", "audio note", "record", "recording", "memo", "microphone", "save audio"],
            ExampleUtterances = ["record a voice note", "start an audio memo", "make a voice recording"]
        },
        new GlanceActionDescriptor("VoiceNotes.Stop", Id, "Stop voice note", "Stop and save the current voice-note recording.")
        {
            SemanticTags = ["voice note", "audio note", "record", "recording", "memo", "stop", "save"],
            ExampleUtterances = ["stop the voice note", "finish recording my memo", "save this voice recording"]
        }
    ];

    public bool IsAvailable(string actionId) =>
        actionId switch
        {
            "VoiceNotes.Start" => !recordingService.IsRecording,
            "VoiceNotes.Stop" => recordingService.IsRecording,
            _ => false
        };

    public Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ActionId is not ("VoiceNotes.Start" or "VoiceNotes.Stop"))
        {
            return Task.FromResult(GlanceActionResult.Unavailable());
        }

        viewModel.ToggleRecording();
        return Task.FromResult(GlanceActionResult.Success());
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= HandleTick;
        viewModel.RecordingToggleRequested -= HandleRecordingToggleRequested;
        viewModel.OpenRequested -= HandleOpenRequested;
        viewModel.DeleteRequested -= HandleDeleteRequested;
        recordingService.LevelsChanged -= HandleLevelsChanged;
        recordingService.RecordingCompleted -= HandleRecordingCompleted;
        options.Changed -= HandleOptionsChanged;

        if (recordingService.IsRecording)
        {
            recordingService.StopRecording();
        }
    }

    private int RecentRecordingLimit => (int)Math.Clamp(options.Current.RecentRecordingLimit, 1, 10);

    private void HandleOptionsChanged(object? sender, GlanceModuleOptionsChangedEventArgs<VoiceNotesSettings> args) =>
        dispatcherQueue.TryEnqueue(() => viewModel.ApplySettings(args.Options));

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

    private void HandleLevelsChanged(object? sender,
        VoiceLevelsChangedEventArgs args) =>
        dispatcherQueue.TryEnqueue(() =>
        {
            double averageLevel = 0;
            double peakLevel = 0;

            foreach (double sample in args.Levels)
            {
                averageLevel += sample;
                peakLevel = Math.Max(peakLevel, sample);
            }

            averageLevel = args.Levels.Count == 0
                ? 0
                : averageLevel / args.Levels.Count;
            double level = Math.Clamp((averageLevel * 0.78) + (peakLevel * 0.34), 0, 1);
            Array.Copy(waveformHistory, 1, waveformHistory, 0, waveformHistory.Length - 1);
            waveformHistory[^1] = Math.Clamp(level, 0, 1);
            viewModel.UpdateAudioLevels(waveformHistory);
        });

    private void HandleRecordingCompleted(object? sender,
        VoiceRecordingCompletedEventArgs args) =>
        dispatcherQueue.TryEnqueue(() =>
        {
            timer.Stop();
            recordingStartedTimestamp = 0;
            Array.Clear(waveformHistory);
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
            viewModel.UpdateElapsed(Stopwatch.GetElapsedTime(recordingStartedTimestamp));
        }
    }
}
