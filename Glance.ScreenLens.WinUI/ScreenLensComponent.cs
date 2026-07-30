using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;

namespace Glance.ScreenLens.WinUI;

public sealed partial class ScreenLensComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly IScreenLensService screenLensService;
    private readonly ITextLocalizer localizer;
    private readonly ScreenLensViewModel viewModel;
    private readonly DispatcherQueue dispatcherQueue;

    public ScreenLensComponent(ScreenLensViewModel viewModel,
        IScreenLensService screenLensService,
        ModuleResourceTextLocalizer<ScreenLensModule> localizer)
    {
        this.viewModel = viewModel;
        this.screenLensService = screenLensService;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        ScreenLensCompactView compactView = new(viewModel);
        ScreenLensExpandedView expandedView = new(viewModel);
        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        viewModel.ExtractionRequested += HandleExtractionRequested;
    }

    public string Id => "ScreenLens";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 210;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public void Dispose()
    {
        viewModel.ExtractionRequested -= HandleExtractionRequested;
    }

    private async void HandleExtractionRequested(object? sender, EventArgs args)
    {
        try
        {
            await screenLensService.ExtractAsync();
        }
        catch
        { }
        finally
        {
            dispatcherQueue.TryEnqueue(viewModel.Complete);
        }
    }
}
