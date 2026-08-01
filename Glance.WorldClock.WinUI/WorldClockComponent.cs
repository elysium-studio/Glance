using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;

namespace Glance.WorldClock.WinUI;

public sealed partial class WorldClockComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly DispatcherQueueTimer timer;
    private readonly ITextLocalizer localizer;
    private readonly GlanceModuleOptions<WorldClockSettings> options;
    private readonly TimeProvider timeProvider;
    private readonly WorldClockViewModel viewModel;

    public WorldClockComponent(WorldClockViewModel viewModel,
        TimeProvider timeProvider,
        GlanceModuleOptions<WorldClockSettings> options,
        ModuleResourceTextLocalizer<WorldClockModule> localizer)
    {
        this.viewModel = viewModel;
        this.timeProvider = timeProvider;
        this.options = options;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        WorldClockCompactView compactView = new(viewModel);
        WorldClockExpandedView expandedView = new(viewModel, localizer);
        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        timer = dispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.IsRepeating = true;
        timer.Tick += HandleTick;
        timer.Start();
        options.Changed += HandleOptionsChanged;
        Refresh();
    }

    public string Id => "WorldClock";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 15;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= HandleTick;
        options.Changed -= HandleOptionsChanged;
    }

    private void HandleTick(DispatcherQueueTimer sender, object args) => Refresh();

    private void HandleOptionsChanged(object? sender, GlanceModuleOptionsChangedEventArgs<WorldClockSettings> args) =>
        dispatcherQueue.TryEnqueue(Refresh);

    private void Refresh() => viewModel.Refresh(timeProvider.GetUtcNow(), options.Current.Use24HourTime);
}
