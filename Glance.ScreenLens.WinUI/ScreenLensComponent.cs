using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;

namespace Glance.ScreenLens.WinUI;

public sealed partial class ScreenLensComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IGlanceAttentionComponent,
    IDisposable
{
    private readonly IGlanceAttentionService attentionService;
    private readonly IScreenLensService screenLensService;
    private readonly ITextLocalizer localizer;
    private readonly ScreenLensViewModel viewModel;
    private readonly DispatcherQueue dispatcherQueue;

    public ScreenLensComponent(ScreenLensViewModel viewModel,
        IScreenLensService screenLensService,
        IGlanceAttentionService attentionService,
        ModuleResourceTextLocalizer<ScreenLensModule> localizer)
    {
        this.viewModel = viewModel;
        this.screenLensService = screenLensService;
        this.attentionService = attentionService;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        ScreenLensCompactView compactView = new(viewModel);
        ScreenLensExpandedView expandedView = new(viewModel);
        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        viewModel.ExtractionRequested += HandleExtractionRequested;
        viewModel.CopyRequested += HandleCopyRequested;
    }

    public string Id => "ScreenLens";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 210;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public bool IsAttentionEnabledByDefault => true;

    public void Dispose()
    {
        viewModel.ExtractionRequested -= HandleExtractionRequested;
        viewModel.CopyRequested -= HandleCopyRequested;
    }

    private async void HandleExtractionRequested(object? sender, EventArgs args)
    {
        try
        {
            ScreenLensResult? result = await screenLensService.ExtractAsync();

            if (result is null)
            {
                dispatcherQueue.TryEnqueue(viewModel.Cancel);
                return;
            }

            dispatcherQueue.TryEnqueue(() =>
            {
                viewModel.Complete(result);
                attentionService.RequestAttention(Id);
            });
        }
        catch
        {
            dispatcherQueue.TryEnqueue(viewModel.Fail);
        }
    }

    private async void HandleCopyRequested(object? sender, EventArgs args) =>
        await screenLensService.CopyAsync(viewModel.ExtractedText);
}
