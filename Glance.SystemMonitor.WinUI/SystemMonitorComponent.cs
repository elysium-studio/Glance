using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;

namespace Glance.SystemMonitor.WinUI;

public sealed class SystemMonitorComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly DispatcherQueueTimer timer;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ITextLocalizer localizer;
    private readonly SystemMetricsReader metricsReader = new();
    private readonly SystemMonitorViewModel viewModel;
    private readonly GlanceModuleOptions<SystemMonitorSettings> options;

    public SystemMonitorComponent(
        SystemMonitorViewModel viewModel,
        GlanceModuleOptions<SystemMonitorSettings> options,
        ModuleResourceTextLocalizer<SystemMonitorModule> localizer)
    {
        this.viewModel = viewModel;
        this.options = options;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        SystemMonitorCompactView compactView = new(viewModel);
        SystemMonitorExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        timer = dispatcherQueue.CreateTimer();
        timer.Interval = GetRefreshInterval(options.Current);
        timer.IsRepeating = true;
        timer.Tick += HandleTick;
        timer.Start();
        options.Changed += HandleOptionsChanged;

        UpdateMetrics();
    }

    public string Id => "SystemMonitor";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 30;

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

    private void HandleTick(DispatcherQueueTimer sender, object args) => UpdateMetrics();

    private void HandleOptionsChanged(object? sender, GlanceModuleOptionsChangedEventArgs<SystemMonitorSettings> args) =>
        dispatcherQueue.TryEnqueue(() => timer.Interval = GetRefreshInterval(args.Options));

    private static TimeSpan GetRefreshInterval(SystemMonitorSettings settings) =>
        TimeSpan.FromSeconds(Math.Clamp(settings.RefreshIntervalSeconds, 0.5, 10));

    private void UpdateMetrics()
    {
        SystemMetrics metrics = metricsReader.Read();
        viewModel.Update(metrics.CpuUsage, metrics.MemoryUsage, metrics.UsedMemory, metrics.TotalMemory, metrics.DownloadBytesPerSecond, metrics.UploadBytesPerSecond);
    }
}
