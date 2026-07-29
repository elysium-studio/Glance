using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Glance.SystemMonitor.WinUI;

public sealed partial class SystemMonitorExpandedView :
    UserControl
{
    public SystemMonitorExpandedView(SystemMonitorViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ViewModel.MetricsUpdated += HandleMetricsUpdated;
        AddCurrentSample();
    }

    public SystemMonitorViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private void HandleMetricsUpdated(object? sender, EventArgs args) => DispatcherQueue.TryEnqueue(AddCurrentSample);

    private void AddCurrentSample()
    {
        CpuGraph.AddSample(ViewModel.CpuUsage);
        MemoryGraph.AddSample(ViewModel.MemoryUsage);
        NetworkGraph.AddSample(ViewModel.DownloadBytesPerSecond, ViewModel.UploadBytesPerSecond);
    }
}
