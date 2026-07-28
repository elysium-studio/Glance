using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;

namespace Glance.Magnifier.WinUI;

public sealed partial class MagnifierComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly IMagnifierService magnifierService;
    private readonly DispatcherQueueTimer refreshTimer;

    public MagnifierComponent(MagnifierViewModel viewModel,
        IMagnifierService magnifierService,
        ModuleResourceTextLocalizer<MagnifierModule> localizer)
    {
        this.magnifierService = magnifierService;
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

    public int Order => 200;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public void Dispose()
    {
        refreshTimer.Stop();
        magnifierService.Dispose();
        GC.SuppressFinalize(this);
    }
}
