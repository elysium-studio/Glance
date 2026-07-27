using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Glance.Stopwatch.WinUI;

public sealed partial class StopwatchComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly DispatcherQueueTimer timer;
    private readonly ITextLocalizer localizer;
    private readonly StopwatchViewModel viewModel;
    private readonly IWritableOptions<StopwatchSettings> writer;

    public StopwatchComponent(StopwatchViewModel viewModel,
        IWritableOptions<StopwatchSettings> writer,
        ModuleResourceTextLocalizer<StopwatchModule> localizer)
    {
        this.viewModel = viewModel;
        this.writer = writer;
        this.localizer = localizer;

        StopwatchCompactView compactView = new(viewModel);
        StopwatchExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(30);
        timer.IsRepeating = true;
        timer.Tick += HandleTick;

        viewModel.PropertyChanged += HandlePropertyChanged;
        viewModel.SessionStateChanged += HandleSessionStateChanged;

        if (viewModel.IsRunning)
        {
            timer.Start();
        }

        _ = PersistSessionAsync();
    }

    public string Id => "Stopwatch";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 0;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= HandleTick;
        viewModel.PropertyChanged -= HandlePropertyChanged;
        viewModel.SessionStateChanged -= HandleSessionStateChanged;
    }

    private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(StopwatchViewModel.IsRunning))
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

    private void HandleTick(DispatcherQueueTimer sender, object args) =>
        viewModel.Refresh();

    private async void HandleSessionStateChanged(object? sender, EventArgs args) =>
        await PersistSessionAsync();

    private async Task PersistSessionAsync()
    {
        try
        {
            await writer.WriteAsync(settings => viewModel.WriteSessionState(settings));
        }
        catch
        { }
    }
}
