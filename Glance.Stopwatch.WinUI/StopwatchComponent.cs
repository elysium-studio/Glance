using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Stopwatch.WinUI;

public sealed partial class StopwatchComponent :
    IGlanceComponent,
    IGlanceActionProvider,
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

    public IReadOnlyList<GlanceActionDescriptor> GetActions() =>
    [
        new GlanceActionDescriptor("Stopwatch.Start", Id, "Start stopwatch", "Start or resume the stopwatch."),
        new GlanceActionDescriptor("Stopwatch.Pause", Id, "Pause stopwatch", "Pause the running stopwatch."),
        new GlanceActionDescriptor("Stopwatch.Reset", Id, "Reset stopwatch", "Reset the stopwatch to zero.")
    ];

    public bool IsAvailable(string actionId) =>
        actionId switch
        {
            "Stopwatch.Start" => !viewModel.IsRunning,
            "Stopwatch.Pause" => viewModel.IsRunning,
            _ => true
        };

    public Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ActionId is "Stopwatch.Start" or "Stopwatch.Pause")
        {
            viewModel.Toggle();
        }
        else if (request.ActionId == "Stopwatch.Reset")
        {
            viewModel.Reset();
        }
        else
        {
            return Task.FromResult(GlanceActionResult.Unavailable());
        }

        return Task.FromResult(GlanceActionResult.Success());
    }

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
