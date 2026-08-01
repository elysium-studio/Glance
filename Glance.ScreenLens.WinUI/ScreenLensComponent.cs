using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.ScreenLens.WinUI;

public sealed partial class ScreenLensComponent :
    IGlanceComponent,
    IGlanceActionProvider,
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

    public IReadOnlyList<GlanceActionDescriptor> GetActions() =>
    [
        new GlanceActionDescriptor("ScreenLens.Extract", Id, "Extract text from the screen", "Select a region and extract its text.")
    ];

    public bool IsAvailable(string actionId) => !viewModel.IsExtracting;

    public async Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ActionId != "ScreenLens.Extract")
        {
            return GlanceActionResult.Unavailable();
        }

        viewModel.IsExtracting = true;

        try
        {
            await screenLensService.ExtractAsync();
            return GlanceActionResult.Success();
        }
        finally
        {
            viewModel.Complete();
        }
    }

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
