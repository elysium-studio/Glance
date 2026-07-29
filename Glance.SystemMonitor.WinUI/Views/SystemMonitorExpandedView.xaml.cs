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

    public void SetSampleInterval(TimeSpan interval)
    {
        CpuGraph.SampleInterval = interval;
        MemoryGraph.SampleInterval = interval;
        NetworkGraph.SampleInterval = interval;
    }

    private void HandleMetricsUpdated(object? sender, EventArgs args) => AddCurrentSample();

    private void AddCurrentSample()
    {
        CpuGraph.AddSample(ViewModel.CpuUsage);
        MemoryGraph.AddSample(ViewModel.MemoryUsage);
        NetworkGraph.AddSample(ViewModel.DownloadBytesPerSecond, ViewModel.UploadBytesPerSecond);
    }
}
