using Glance.Application.Abstractions;
using Microsoft.UI.Dispatching;
using System;

namespace Glance.SystemMonitor.WinUI;

public sealed class SystemMonitorComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly DispatcherQueueTimer timer;
    private readonly SystemMetricsReader metricsReader = new();
    private readonly SystemMonitorViewModel viewModel;

    public SystemMonitorComponent(SystemMonitorViewModel viewModel)
    {
        this.viewModel = viewModel;

        SystemMonitorCompactView compactView = new(viewModel);
        SystemMonitorExpandedView expandedView = new(viewModel);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.IsRepeating = true;
        timer.Tick += HandleTick;
        timer.Start();

        UpdateMetrics();
    }

    public string Id => "SystemMonitor";

    public int Order => 30;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= HandleTick;
    }

    private void HandleTick(DispatcherQueueTimer sender, object args) => UpdateMetrics();

    private void UpdateMetrics()
    {
        SystemMetrics metrics = metricsReader.Read();
        viewModel.Update(
            metrics.CpuUsage,
            metrics.MemoryUsage,
            metrics.UsedMemory,
            metrics.TotalMemory,
            metrics.DownloadBytesPerSecond,
            metrics.UploadBytesPerSecond);
    }
}
