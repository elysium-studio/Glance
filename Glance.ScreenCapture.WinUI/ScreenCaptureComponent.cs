using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;

namespace Glance.ScreenCapture.WinUI;

public sealed class ScreenCaptureComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly IGlanceAttentionService attentionService;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ITextLocalizer localizer;
    private readonly IScreenCaptureService screenCaptureService;
    private readonly ScreenCaptureViewModel viewModel;

    public ScreenCaptureComponent(
        ScreenCaptureViewModel viewModel,
        IScreenCaptureService screenCaptureService,
        IGlanceAttentionService attentionService,
        ModuleResourceTextLocalizer<ScreenCaptureModule> localizer)
    {
        this.viewModel = viewModel;
        this.screenCaptureService = screenCaptureService;
        this.attentionService = attentionService;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        ScreenCaptureCompactView compactView = new(viewModel);
        ScreenCaptureExpandedView expandedView = new(viewModel);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        viewModel.CaptureRequested += HandleCaptureRequested;
        viewModel.OpenRequested += HandleOpenRequested;
        viewModel.RevealRequested += HandleRevealRequested;
        viewModel.CopyRequested += HandleCopyRequested;
        viewModel.DeleteRequested += HandleDeleteRequested;
        viewModel.SetCaptures(screenCaptureService.GetRecentCaptures(6));
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
    }

    private async void HandleCaptureRequested(object? sender, ScreenCaptureMode mode)
    {
        try
        {
            ScreenCaptureItem? capture = await screenCaptureService.CaptureAsync(mode);
            viewModel.CompleteCapture(capture);

            if (capture is not null)
            {
                attentionService.RequestAttention(Id);
            }
        }
        catch
        {
            viewModel.ShowCaptureError();
        }
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
}
