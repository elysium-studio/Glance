using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Glance.Timer.WinUI;

public sealed partial class TimerComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IGlanceAttentionComponent,
    IDisposable
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly DispatcherQueueTimer timer;
    private readonly ITextLocalizer localizer;
    private readonly TimerViewModel viewModel;
    private readonly IGlanceAttentionService attentionService;
    private readonly GlanceModuleOptions<TimerSettings> options;
    private readonly IWritableOptions<TimerSettings> writer;

    public TimerComponent(TimerViewModel viewModel,
        IGlanceAttentionService attentionService,
        GlanceModuleOptions<TimerSettings> options,
        IWritableOptions<TimerSettings> writer,
        ModuleResourceTextLocalizer<TimerModule> localizer)
    {
        this.viewModel = viewModel;
        this.attentionService = attentionService;
        this.options = options;
        this.writer = writer;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        TimerCompactView compactView = new(viewModel);
        TimerExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        timer = dispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(100);
        timer.IsRepeating = true;
        timer.Tick += HandleTick;

        viewModel.PropertyChanged += HandlePropertyChanged;
        viewModel.SessionStateChanged += HandleSessionStateChanged;
        options.Changed += HandleOptionsChanged;

        if (viewModel.IsRunning)
        {
            timer.Start();
        }

        _ = PersistSessionAsync();
    }

    public string Id => "Timer";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 10;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public bool IsAttentionEnabledByDefault => true;

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= HandleTick;
        viewModel.PropertyChanged -= HandlePropertyChanged;
        viewModel.SessionStateChanged -= HandleSessionStateChanged;
        options.Changed -= HandleOptionsChanged;
    }

    private void HandleOptionsChanged(object? sender, GlanceModuleOptionsChangedEventArgs<TimerSettings> args) =>
        dispatcherQueue.TryEnqueue(() => viewModel.ApplySettings(args.Options));

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
