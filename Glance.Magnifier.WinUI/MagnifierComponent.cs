using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Magnifier.WinUI;

public sealed partial class MagnifierComponent :
    IGlanceComponent,
    IGlanceActionProvider,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly IMagnifierService magnifierService;
    private readonly DispatcherQueueTimer refreshTimer;
    private readonly MagnifierViewModel viewModel;

    public MagnifierComponent(MagnifierViewModel viewModel,
        IMagnifierService magnifierService,
        ModuleResourceTextLocalizer<MagnifierModule> localizer)
    {
        this.magnifierService = magnifierService;
        this.viewModel = viewModel;
        viewModel.Refresh();

        MagnifierCompactView compactView = new(viewModel);
        MagnifierExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;
        DisplayName = localizer.GetText("ModuleDisplayName");
        Description = localizer.GetText("ModuleDescription");

        refreshTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        refreshTimer.Interval = TimeSpan.FromMilliseconds(200);
        refreshTimer.Tick += (_, _) => viewModel.Refresh();
        refreshTimer.Start();
    }

    public string Id => "Magnifier";

    public string DisplayName { get; }

    public string Description { get; }

    public string SettingsCategory => GlanceModuleCategories.MediaAndCapture;

    public int Order => 200;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public IReadOnlyList<GlanceActionDescriptor> GetActions() => [
        new GlanceActionDescriptor("Magnifier.Start", Id, "Start Magnifier", "Start the Windows screen Magnifier accessibility tool.")
        {
            SemanticTags = ["magnifier", "magnify", "screen zoom", "accessibility", "enlarge", "make bigger"],
            ExampleUtterances = ["start the magnifier", "magnify my screen", "make the screen easier to see"]
        },
        new GlanceActionDescriptor("Magnifier.ZoomIn", Id, "Zoom in", "Increase the Windows Magnifier screen zoom level.")
        {
            SemanticTags = ["magnifier", "magnify", "zoom in", "enlarge", "bigger"],
            ExampleUtterances = ["zoom the magnifier in", "make the screen bigger", "increase magnification"]
        },
        new GlanceActionDescriptor("Magnifier.ZoomOut", Id, "Zoom out", "Decrease the Windows Magnifier screen zoom level.")
        {
            SemanticTags = ["magnifier", "magnify", "zoom out", "smaller", "decrease"],
            ExampleUtterances = ["zoom the magnifier out", "make the screen smaller", "decrease magnification"]
        },
        new GlanceActionDescriptor("Magnifier.Stop", Id, "Stop Magnifier", "Close the Windows screen Magnifier.")
        {
            SemanticTags = ["magnifier", "magnify", "close", "stop", "turn off"],
            ExampleUtterances = ["close the magnifier", "turn off screen magnification", "stop magnifying"]
        }
    ];

    public bool IsAvailable(string actionId) => actionId switch
    {
        "Magnifier.Start" => viewModel.CanStart,
        "Magnifier.ZoomIn" => viewModel.CanZoomIn,
        "Magnifier.ZoomOut" => viewModel.CanZoomOut,
        "Magnifier.Stop" => viewModel.CanClose,
        _ => false
    };

    public Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        switch (request.ActionId)
        {
            case "Magnifier.Start":
                viewModel.Start();
                break;
            case "Magnifier.ZoomIn":
                viewModel.ZoomIn();
                break;
            case "Magnifier.ZoomOut":
                viewModel.ZoomOut();
                break;
            case "Magnifier.Stop":
                viewModel.Close();
                break;
            default:
                return Task.FromResult(GlanceActionResult.Unavailable());
        }

        viewModel.Refresh();
        return Task.FromResult(GlanceActionResult.Success());
    }

    public void Dispose()
    {
        refreshTimer.Stop();
        magnifierService.Dispose();
        GC.SuppressFinalize(this);
    }
}
