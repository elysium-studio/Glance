using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Glance.Network.WinUI;

public sealed partial class NetworkExpandedView :
    UserControl
{
    public NetworkExpandedView(NetworkViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ViewModel.MetricsUpdated += HandleMetricsUpdated;
        AddCurrentSample();
    }

    public NetworkViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private void HandleMetricsUpdated(object? sender, EventArgs args) => AddCurrentSample();

    private void AddCurrentSample()
    {
        DownloadGraph.AddSample(ViewModel.DownloadBytesPerSecond);
        UploadGraph.AddSample(ViewModel.UploadBytesPerSecond);
    }

}
