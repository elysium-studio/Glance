using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Glance.SystemMonitor.WinUI;

public sealed partial class SystemMonitorExpandedView :
    UserControl
{
    public static readonly DependencyProperty ShowPerformanceChartsProperty = DependencyProperty.Register(nameof(ShowPerformanceCharts), typeof(bool), typeof(SystemMonitorExpandedView), new PropertyMetadata(true));

    public SystemMonitorExpandedView(SystemMonitorViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ViewModel.MetricsUpdated += HandleMetricsUpdated;
        AddCurrentSample();
    }

    public SystemMonitorViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public bool ShowPerformanceCharts
    {
        get => (bool)GetValue(ShowPerformanceChartsProperty);
        set => SetValue(ShowPerformanceChartsProperty, value);
    }

    public void SetSampleInterval(TimeSpan interval)
    {
        CpuGraph.SampleInterval = interval;
        MemoryGraph.SampleInterval = interval;
        GpuGraph.SampleInterval = interval;
    }

    private void HandleMetricsUpdated(object? sender, EventArgs args) => AddCurrentSample();

    private void AddCurrentSample()
    {
        CpuGraph.AddSample(ViewModel.CpuUsage);
        MemoryGraph.AddSample(ViewModel.MemoryUsage);
        GpuGraph.AddSample(ViewModel.GpuUsage);
    }

    private Visibility WhenShowingCharts(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    private Visibility WhenHidingCharts(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
}
