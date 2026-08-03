using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.ScreenCapture.WinUI;

public sealed partial class ScreenCaptureComponent :
    IGlanceComponent,
    IGlanceActionProvider,
    IGlanceActionValidator,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ITextLocalizer localizer;
    private readonly ILogger<ScreenCaptureComponent> logger;
    private readonly IScreenCaptureService screenCaptureService;
    private readonly ScreenCaptureExpandedView expandedView;
    private readonly ScreenCaptureViewModel viewModel;
    private readonly GlanceModuleOptions<ScreenCaptureSettings> options;
    private bool captureRefreshPending;

    public ScreenCaptureComponent(ScreenCaptureViewModel viewModel,
        IScreenCaptureService screenCaptureService,
        GlanceModuleOptions<ScreenCaptureSettings> options,
        ModuleResourceTextLocalizer<ScreenCaptureModule> localizer,
        ILogger<ScreenCaptureComponent> logger)
    {
        this.viewModel = viewModel;
        this.screenCaptureService = screenCaptureService;
        this.options = options;
        this.localizer = localizer;
        this.logger = logger;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        ScreenCaptureCompactView compactView = new(viewModel);
        expandedView = new ScreenCaptureExpandedView(viewModel);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        viewModel.CaptureRequested += HandleCaptureRequested;
        viewModel.OpenRequested += HandleOpenRequested;
        viewModel.RevealRequested += HandleRevealRequested;
        viewModel.CopyRequested += HandleCopyRequested;
        viewModel.DeleteRequested += HandleDeleteRequested;
        screenCaptureService.CapturesChanged += HandleCapturesChanged;
        options.Changed += HandleOptionsChanged;
        viewModel.SetCaptures(screenCaptureService.GetRecentCaptures(RecentCaptureLimit));
    }

    public string Id => "ScreenCapture";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 110;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public IReadOnlyList<GlanceActionDescriptor> GetActions() =>
    [
        new GlanceActionDescriptor("ScreenCapture.Region", Id, "Capture a region", "Select and capture a region of the screen.", GlanceActionPresentation.Expanded)
        {
            SemanticTags = ["screenshot", "screen shot", "screen capture", "snip", "region", "area", "selection"],
            ExampleUtterances = ["take a screenshot of an area", "capture a region of my screen", "let me select an area to capture"]
        },
        new GlanceActionDescriptor("ScreenCapture.Window",
            Id,
            "Capture a window",
            "Take a screenshot using the window picker, optionally selecting a window by title. Use this for a generic screenshot request when no region, display, or all-displays mode was requested.",
            [new GlanceActionParameterDescriptor("window", GlanceActionParameterType.String, "Part or all of the window title.", IsRequired: false)],
            Presentation: GlanceActionPresentation.Expanded)
        {
            SemanticTags = ["screenshot", "screen shot", "screens hot", "screen capture", "capture", "window", "app"],
            ExampleUtterances = ["take a screenshot for me", "take a screen shot", "capture a window", "take a screenshot of Visual Studio"]
        },
        new GlanceActionDescriptor("ScreenCapture.Display", Id, "Capture a display", "Select and capture a display.", GlanceActionPresentation.Expanded)
        {
            SemanticTags = ["screenshot", "screen shot", "screen capture", "full screen", "display", "monitor"],
            ExampleUtterances = ["take a full screen screenshot", "capture this display", "take a screenshot of my monitor"]
        },
        new GlanceActionDescriptor("ScreenCapture.AllDisplays", Id, "Capture all displays", "Capture the complete desktop across all displays.", GlanceActionPresentation.Expanded)
        {
            SemanticTags = ["screenshot", "screen shot", "screen capture", "all displays", "all monitors", "entire desktop"],
            ExampleUtterances = ["capture all displays", "take a screenshot of every monitor", "capture my entire desktop"]
        }
    ];

    public bool IsAvailable(string actionId) => !viewModel.IsCapturing;

    public Task<GlanceActionResult?> ValidateAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.ActionId, "ScreenCapture.Window", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<GlanceActionResult?>(null);
        }

        string? windowName = request.GetString("window");

        if (string.IsNullOrWhiteSpace(windowName))
        {
            return Task.FromResult<GlanceActionResult?>(null);
        }

        int matches = screenCaptureService.CountMatchingWindows(windowName);

        return Task.FromResult<GlanceActionResult?>(matches switch
        {
            0 => GlanceActionResult.InvalidArguments($"I couldn't find “{windowName}”.", "Try another window name."),
            > 1 => GlanceActionResult.InvalidArguments($"Several windows match “{windowName}”.", "Try a more specific window name."),
            _ => null
        });
    }

    public async Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        ScreenCaptureMode mode = request.ActionId switch
        {
            "ScreenCapture.Region" => ScreenCaptureMode.Region,
            "ScreenCapture.Window" => ScreenCaptureMode.Window,
            "ScreenCapture.Display" => ScreenCaptureMode.Display,
            "ScreenCapture.AllDisplays" => ScreenCaptureMode.AllDisplays,
            _ => (ScreenCaptureMode)(-1)
        };

        if ((int)mode < 0)
        {
            return GlanceActionResult.Unavailable();
        }

        expandedView.SetCaptureInProgress(true);
        viewModel.IsCapturing = true;
        viewModel.StatusText = localizer.GetText("SelectingCapture");
        bool captured = await CaptureAsync(mode, request.GetString("window"));
        return captured
            ? GlanceActionResult.Success()
            : GlanceActionResult.Failed("The capture was cancelled or the requested window could not be found.");
    }

    public void Dispose()
    {
        viewModel.CaptureRequested -= HandleCaptureRequested;
        viewModel.OpenRequested -= HandleOpenRequested;
        viewModel.RevealRequested -= HandleRevealRequested;
        viewModel.CopyRequested -= HandleCopyRequested;
        viewModel.DeleteRequested -= HandleDeleteRequested;
        screenCaptureService.CapturesChanged -= HandleCapturesChanged;
        options.Changed -= HandleOptionsChanged;
    }

    private int RecentCaptureLimit => (int)Math.Clamp(options.Current.RecentCaptureLimit, 1, 12);

    private void HandleOptionsChanged(object? sender, GlanceModuleOptionsChangedEventArgs<ScreenCaptureSettings> args) =>
        dispatcherQueue.TryEnqueue(() => viewModel.ApplySettings(args.Options));

    private void HandleCaptureRequested(object? sender, ScreenCaptureMode mode)
    {
        expandedView.SetCaptureInProgress(true);

        if (!dispatcherQueue.TryEnqueue(async () => await CaptureAsync(mode)))
        {
            expandedView.SetCaptureInProgress(false);
            viewModel.ShowCaptureError();
        }
    }

    private async Task<bool> CaptureAsync(ScreenCaptureMode mode, string? windowName = null)
    {
        try
        {
            ScreenCaptureItem? capture = mode == ScreenCaptureMode.Window && !string.IsNullOrWhiteSpace(windowName)
                ? await screenCaptureService.CaptureWindowAsync(windowName)
                : await screenCaptureService.CaptureAsync(mode);

            if (capture is null)
            {
                RunOnUiThread(() =>
                {
                    viewModel.CompleteCapture(null);
                    expandedView.SetCaptureInProgress(false);
                    ApplyPendingCaptureRefresh();
                });
                return false;
            }

            Task<bool>? automaticCopyTask = options.Current.CopyToClipboardAutomatically
                ? screenCaptureService.TryCopyAsync(capture)
                : null;
            CaptureAnimationFrame? frame = (screenCaptureService as WindowsScreenCaptureService)?.TakeAnimationFrame();
            NativeRectangle? landingBounds = await GetLandingBoundsAsync();
            bool capturePresented = false;

            void PresentCapture()
            {
                if (capturePresented)
                {
                    return;
                }

                capturePresented = true;
                viewModel.CompleteCapture(capture);
                ApplyPendingCaptureRefresh();
            }

            if (frame is not null && landingBounds is NativeRectangle target)
            {
                try
                {
                    await frame.Overlay.PlayFlightAsync(frame.Bitmap, target, PresentCapture);
                }
                catch (Exception exception)
                {
                    frame.Overlay.Close();
                    logger.LogWarning(exception, "The capture flight animation could not be displayed ({ErrorCode:X8}): {ErrorMessage}", exception.HResult, exception.Message);
                }
            }
            else if (frame is null)
            {
                logger.LogWarning("The capture flight animation was skipped because no animation frame was available");
            }
            else
            {
                frame.Overlay.Close();
                logger.LogWarning("The capture flight animation was skipped because the island landing target was unavailable");
            }

            RunOnUiThread(() =>
            {
                PresentCapture();
                expandedView.CompleteCapturePresentation();
            });

            if (automaticCopyTask is not null && !await automaticCopyTask)
            {
                logger.LogWarning("The completed screen capture could not be copied to the clipboard automatically");
            }

            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to capture the screen using {CaptureMode}", mode);
            RunOnUiThread(() =>
            {
                expandedView.SetCaptureInProgress(false);
                viewModel.ShowCaptureError();
                ApplyPendingCaptureRefresh();
            });
            return false;
        }
    }

    private async Task<NativeRectangle?> GetLandingBoundsAsync()
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            NativeRectangle? bounds = await RunOnUiThreadAsync<NativeRectangle?>(() => expandedView.TryGetCaptureLandingBounds(out NativeRectangle value) ? value : null);

            if (bounds is not null)
            {
                return bounds;
            }

            await Task.Delay(40);
        }

        return null;
    }

    private Task<T> RunOnUiThreadAsync<T>(Func<T> action)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            return Task.FromResult(action());
        }

        TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                completion.TrySetResult(action());
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }))
        {
            completion.TrySetException(new InvalidOperationException("Unable to access the screen capture view."));
        }

        return completion.Task;
    }

    private void RunOnUiThread(Action action)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        dispatcherQueue.TryEnqueue(() => action());
    }

    private void HandleOpenRequested(object? sender, ScreenCaptureItem capture) =>
        screenCaptureService.TryOpen(capture);

    private void HandleRevealRequested(object? sender, ScreenCaptureItem capture) =>
        screenCaptureService.TryReveal(capture);

    private async void HandleCopyRequested(object? sender, ScreenCaptureItem capture) =>
        await screenCaptureService.TryCopyAsync(capture);

    private void HandleDeleteRequested(object? sender, ScreenCaptureItem capture)
    {
        if (screenCaptureService.TryDelete(capture))
        {
            dispatcherQueue.TryEnqueue(() => viewModel.Remove(capture));
        }
    }

    private void HandleCapturesChanged(object? sender, EventArgs args) =>
        dispatcherQueue.TryEnqueue(() =>
        {
            if (viewModel.IsCapturing)
            {
                captureRefreshPending = true;
                return;
            }

            RefreshCaptures();
        });

    private void ApplyPendingCaptureRefresh()
    {
        if (!captureRefreshPending)
        {
            return;
        }

        captureRefreshPending = false;
        RefreshCaptures();
    }

    private void RefreshCaptures() =>
        viewModel.SetCaptures(screenCaptureService.GetRecentCaptures(RecentCaptureLimit));
}
