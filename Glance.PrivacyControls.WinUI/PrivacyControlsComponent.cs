using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;

namespace Glance.PrivacyControls.WinUI;

public sealed class PrivacyControlsComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly ITextLocalizer localizer;
    private readonly DispatcherQueueTimer timer;
    private readonly PrivacyControlsViewModel viewModel;

    public PrivacyControlsComponent(
        PrivacyControlsViewModel viewModel,
        ModuleResourceTextLocalizer<PrivacyControlsModule> localizer)
    {
        this.viewModel = viewModel;
        this.localizer = localizer;

        PrivacyControlsCompactView compactView = new(viewModel);
        PrivacyControlsExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(80);
        timer.IsRepeating = true;
        timer.Tick += HandleTick;
        timer.Start();
    }

    public string Id => "PrivacyControls";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 120;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= HandleTick;
    }

    private void HandleTick(DispatcherQueueTimer sender, object args) =>
        viewModel.Refresh();
}
