using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.ComponentModel;

namespace Glance.Timer.WinUI;

public sealed class TimerComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly DispatcherQueueTimer timer;
    private readonly ITextLocalizer localizer;
    private readonly TimerViewModel viewModel;
    private readonly IGlanceAttentionService attentionService;

    public TimerComponent(
        TimerViewModel viewModel,
        IGlanceAttentionService attentionService,
        ModuleResourceTextLocalizer<TimerModule> localizer)
    {
        this.viewModel = viewModel;
        this.attentionService = attentionService;
        this.localizer = localizer;

        TimerCompactView compactView = new(viewModel);
        TimerExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(100);
        timer.IsRepeating = true;
        timer.Tick += HandleTick;

        viewModel.PropertyChanged += HandlePropertyChanged;
    }

    public string Id => "Timer";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 10;

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
        if (args.PropertyName != nameof(TimerViewModel.IsRunning))
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
        if (viewModel.Refresh())
        {
            attentionService.RequestAttention(Id);
        }
    }
}
