using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;

namespace Glance.ScreenRecorder.WinUI;

public sealed partial class ScreenRecorderComponent :
    IGlanceComponent,
    IGlanceActionProvider,
    IGlanceActionValidator,
    IGlanceConnectedAnimationComponent,
    IGlanceExpansionLockComponent,
    IDisposable
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ITextLocalizer localizer;
    private readonly IScreenRecordingService recordingService;
    private readonly ScreenRecorderExpandedView expandedView;
    private readonly ScreenRecorderViewModel viewModel;
    private readonly GlanceModuleOptions<ScreenRecorderSettings> options;

    public ScreenRecorderComponent(ScreenRecorderViewModel viewModel,
        IScreenRecordingService recordingService,
        GlanceModuleOptions<ScreenRecorderSettings> options,
        ModuleResourceTextLocalizer<ScreenRecorderModule> localizer)
    {
        this.viewModel = viewModel;
        this.recordingService = recordingService;
        this.options = options;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        ScreenRecorderCompactView compactView = new(viewModel);
        expandedView = new ScreenRecorderExpandedView(viewModel);
        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        viewModel.RecordingRequested += HandleRecordingRequested;
        viewModel.StopRequested += HandleStopRequested;
        viewModel.OpenRequested += HandleOpenRequested;
        viewModel.RevealRequested += HandleRevealRequested;
        viewModel.DeleteRequested += HandleDeleteRequested;
        recordingService.StateChanged += HandleStateChanged;
        options.Changed += HandleOptionsChanged;
        viewModel.SetRecordings(recordingService.GetRecentRecordings(RecentRecordingLimit));
    }

    public string Id => "ScreenRecorder";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public string SettingsCategory => GlanceModuleCategories.MediaAndCapture;

    public int Order => 115;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public bool IsExpansionLocked { get; private set; }

    public event EventHandler? ExpansionLockChanged;

    public IReadOnlyList<GlanceActionDescriptor> GetActions() => [
        new GlanceActionDescriptor("ScreenRecorder.Region", Id, "Record a region", "Select a region of the screen and record it until stopped.", GlanceActionPresentation.Expanded)
        {
            SemanticTags = ["screen recording", "record screen", "video capture", "region", "area", "selection"],
            ExampleUtterances = ["record an area of my screen", "start recording a region", "record this part of the screen"]
        },
        new GlanceActionDescriptor("ScreenRecorder.Window",
            Id,
            "Record a window",
            "Record a window, optionally selecting it by title. The recording follows that window when it moves or resizes.",
            [new GlanceActionParameterDescriptor("window", GlanceActionParameterType.String, "Part or all of the window title.", IsRequired: false)],
            Presentation: GlanceActionPresentation.Expanded)
        {
            SemanticTags = ["screen recording", "record screen", "video capture", "window", "app"],
            ExampleUtterances = ["record a window", "record Visual Studio", "start a screen recording of this app"]
        },
        new GlanceActionDescriptor("ScreenRecorder.Display", Id, "Record a screen", "Select a display and record the full screen.", GlanceActionPresentation.Expanded)
        {
            SemanticTags = ["screen recording", "record screen", "video capture", "desktop", "display", "monitor", "full screen"],
            ExampleUtterances = ["record my desktop", "start recording this screen", "record this monitor"]
        },
        new GlanceActionDescriptor("ScreenRecorder.Stop", Id, "Stop screen recording", "Stop and save the current screen recording.", GlanceActionPresentation.Expanded)
        {
            SemanticTags = ["screen recording", "record screen", "stop", "finish", "save"],
            ExampleUtterances = ["stop the screen recording", "finish recording my screen", "save the recording"]
        }
    ];

    public bool IsAvailable(string actionId) => actionId == "ScreenRecorder.Stop"
        ? recordingService.State == ScreenRecordingState.Recording
        : recordingService.State is ScreenRecordingState.Idle or ScreenRecordingState.Completed or ScreenRecordingState.Failed;

    public Task<GlanceActionResult?> ValidateAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.ActionId, "ScreenRecorder.Window", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<GlanceActionResult?>(null);
        }

        string? windowName = request.GetString("window");

        if (string.IsNullOrWhiteSpace(windowName))
        {
            return Task.FromResult<GlanceActionResult?>(null);
        }

        int matches = recordingService.CountMatchingWindows(windowName);
        return Task.FromResult<GlanceActionResult?>(matches switch
        {
            0 => GlanceActionResult.InvalidArguments($"I couldn't find a window matching {windowName}.", "Try another window name."),
            > 1 => GlanceActionResult.InvalidArguments($"Several windows match {windowName}.", "Try a more specific window name."),
            _ => null
        });
    }

    public async Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ActionId == "ScreenRecorder.Stop")
        {
            _ = await recordingService.StopAsync(cancellationToken);
            return GlanceActionResult.Success();
        }

        ScreenRecordingMode? mode = request.ActionId switch
        {
            "ScreenRecorder.Region" => ScreenRecordingMode.Region,
            "ScreenRecorder.Window" => ScreenRecordingMode.Window,
            "ScreenRecorder.Display" => ScreenRecordingMode.Display,
            _ => null
        };

        if (mode is null)
        {
            return GlanceActionResult.Unavailable();
        }

        bool started = await recordingService.StartAsync(mode.Value,
            CountdownSeconds,
            options.Current.IncludeCursor,
            request.GetString("window"),
            cancellationToken);
        return started
            ? GlanceActionResult.Success()
            : GlanceActionResult.Failed("The recording was cancelled or the requested source could not be selected.");
    }

    public void DismissExpansionLock()
    {
    }

    public void Dispose()
    {
        viewModel.RecordingRequested -= HandleRecordingRequested;
        viewModel.StopRequested -= HandleStopRequested;
        viewModel.OpenRequested -= HandleOpenRequested;
        viewModel.RevealRequested -= HandleRevealRequested;
        viewModel.DeleteRequested -= HandleDeleteRequested;
        recordingService.StateChanged -= HandleStateChanged;
        options.Changed -= HandleOptionsChanged;
    }

    private int CountdownSeconds => (int)Math.Clamp(options.Current.CountdownSeconds, 0, 10);

    private int RecentRecordingLimit => (int)Math.Clamp(options.Current.RecentRecordingLimit, 1, 12);

    private void HandleOptionsChanged(object? sender, GlanceModuleOptionsChangedEventArgs<ScreenRecorderSettings> args) => _ = dispatcherQueue.TryEnqueue(() => viewModel.ApplySettings(args.Options));

    private async void HandleRecordingRequested(object? sender, ScreenRecordingMode mode) => _ = await recordingService.StartAsync(mode, CountdownSeconds, options.Current.IncludeCursor);

    private async void HandleStopRequested(object? sender, EventArgs args) => _ = await recordingService.StopAsync();

    private void HandleOpenRequested(object? sender, ScreenRecording recording) => _ = recordingService.TryOpen(recording);

    private void HandleRevealRequested(object? sender, ScreenRecording recording) => _ = recordingService.TryReveal(recording);

    private void HandleDeleteRequested(object? sender, ScreenRecording recording)
    {
        if (recordingService.TryDelete(recording))
        {
            viewModel.Remove(recording);
        }
    }

    private void HandleStateChanged(object? sender, ScreenRecordingStateChangedEventArgs args) => _ = dispatcherQueue.TryEnqueue(async () =>
                                                                                                       {
                                                                                                           RecordingAnimationFrame? frame = args.State == ScreenRecordingState.Completed
                                                                                                               ? (recordingService as WindowsScreenRecordingService)?.TakeAnimationFrame()
                                                                                                               : null;

                                                                                                           if (frame is null)
                                                                                                           {
                                                                                                               ApplyState(args);
                                                                                                               return;
                                                                                                           }

                                                                                                           NativeRectangle? landingBounds = await GetLandingBoundsAsync();
                                                                                                           bool presented = false;

                                                                                                           void Present()
                                                                                                           {
                                                                                                               if (presented)
                                                                                                               {
                                                                                                                   return;
                                                                                                               }

                                                                                                               presented = true;
                                                                                                               ApplyState(args);
                                                                                                           }

                                                                                                           if (landingBounds is NativeRectangle target)
                                                                                                           {
                                                                                                               try
                                                                                                               {
                                                                                                                   await frame.Overlay.PlayFlightAsync(target, Present);
                                                                                                               }
                                                                                                               catch
                                                                                                               {
                                                                                                                   frame.Overlay.Close();
                                                                                                               }
                                                                                                           }
                                                                                                           else
                                                                                                           {
                                                                                                               frame.Overlay.Close();
                                                                                                           }

                                                                                                           Present();
                                                                                                       });

    private void ApplyState(ScreenRecordingStateChangedEventArgs args)
    {
        bool nextExpansionLocked = args.State is ScreenRecordingState.Selecting or ScreenRecordingState.CountingDown or ScreenRecordingState.Recording or ScreenRecordingState.Saving;
        bool expansionLockChanged = IsExpansionLocked != nextExpansionLocked;
        IsExpansionLocked = nextExpansionLocked;
        viewModel.ApplyState(args);

        if (expansionLockChanged)
        {
            ExpansionLockChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<NativeRectangle?> GetLandingBoundsAsync()
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            NativeRectangle? bounds = expandedView.TryGetRecordingLandingBounds(out NativeRectangle value) ? value : null;

            if (bounds is not null)
            {
                return bounds;
            }

            await Task.Delay(40);
        }

        return null;
    }
}
