using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.ComponentModel;

namespace Glance.FocusSession.WinUI;

public sealed class FocusSessionComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly IGlanceAttentionService attentionService;
    private readonly ITextLocalizer localizer;
    private readonly DispatcherQueueTimer timer;
    private readonly FocusSessionViewModel viewModel;

    public FocusSessionComponent(
        FocusSessionViewModel viewModel,
        IGlanceAttentionService attentionService,
        ModuleResourceTextLocalizer<FocusSessionModule> localizer)
    {
        this.viewModel = viewModel;
        this.attentionService = attentionService;
        this.localizer = localizer;

        FocusSessionCompactView compactView = new(viewModel);
        FocusSessionExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(250);
        timer.IsRepeating = true;
        timer.Tick += HandleTick;

        viewModel.PropertyChanged += HandlePropertyChanged;
    }

    public string Id => "FocusSession";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 70;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= HandleTick;
        viewModel.PropertyChanged -= HandlePropertyChanged;
    }

    private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(FocusSessionViewModel.IsRunning))
        {
            return;
        }

        if (viewModel.IsRunning)
        {
            timer.Start();
        }
        else
        {
            timer.Stop();
        }
    }

    private void HandleTick(DispatcherQueueTimer sender, object args)
    {
        if (viewModel.Refresh() is not null)
        {
            attentionService.RequestAttention(Id);
        }
    }
}
