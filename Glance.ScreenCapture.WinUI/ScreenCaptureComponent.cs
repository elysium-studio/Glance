using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using System;
using System.Threading.Tasks;

namespace Glance.ScreenCapture.WinUI;

public sealed class ScreenCaptureComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly IGlanceAttentionService attentionService;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ITextLocalizer localizer;
    private readonly ILogger<ScreenCaptureComponent> logger;
    private readonly IScreenCaptureService screenCaptureService;
    private readonly ScreenCaptureExpandedView expandedView;
    private readonly ScreenCaptureViewModel viewModel;
    private readonly GlanceModuleOptions<ScreenCaptureSettings> options;
    private bool captureRefreshPending;

    public ScreenCaptureComponent(
        ScreenCaptureViewModel viewModel,
        IScreenCaptureService screenCaptureService,
        IGlanceAttentionService attentionService,
        GlanceModuleOptions<ScreenCaptureSettings> options,
        ModuleResourceTextLocalizer<ScreenCaptureModule> localizer,
        ILogger<ScreenCaptureComponent> logger)
    {
        this.viewModel = viewModel;
        this.screenCaptureService = screenCaptureService;
        this.attentionService = attentionService;
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

    private async Task CaptureAsync(ScreenCaptureMode mode)
    {
        try
        {
            ScreenCaptureItem? capture = await screenCaptureService.CaptureAsync(mode);

            if (capture is null)
            {
                RunOnUiThread(() =>
                {
                    viewModel.CompleteCapture(null);
                    expandedView.SetCaptureInProgress(false);
                    ApplyPendingCaptureRefresh();
                });
                return;
            }

            CaptureAnimationFrame? frame = (screenCaptureService as WindowsScreenCaptureService)?.TakeAnimationFrame();
            NativeRectangle? landingBounds = await GetLandingBoundsAsync();

            if (frame is not null && landingBounds is NativeRectangle target)
            {
                try
                {
                    await CaptureFlightWindow.PlayAsync(frame, target, dispatcherQueue);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "The capture flight animation could not be displayed ({ErrorCode:X8}): {ErrorMessage}", exception.HResult, exception.Message);
                }
            }
            else if (frame is null)
            {
                logger.LogWarning("The capture flight animation was skipped because no animation frame was available");
            }
            else
            {
                logger.LogWarning("The capture flight animation was skipped because the island landing target was unavailable");
            }

            RunOnUiThread(() =>
            {
                viewModel.CompleteCapture(capture);

                try
                {
                    expandedView.PlayCaptureArrival();
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "The capture arrival animation could not be displayed");
                    expandedView.ResetCaptureArrival();
                }

                expandedView.SetCaptureInProgress(false);
                ApplyPendingCaptureRefresh();
                attentionService.RequestAttention(Id, GlanceAttentionLevel.Passive, expand: false);
            });
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
