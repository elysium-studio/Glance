using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Torrents.WinUI;

public sealed partial class TorrentExpandedView : UserControl
{
    public TorrentExpandedView(TorrentsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public TorrentsViewModel ViewModel { get; }
    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
    public event EventHandler<string>? PauseRequested;
    public event EventHandler<string>? ResumeRequested;
    public event EventHandler<string>? RemoveRequested;
    private void PauseClicked(object sender, RoutedEventArgs args) => PauseRequested?.Invoke(this, ((Button)sender).Tag?.ToString() ?? string.Empty);
    private void ResumeClicked(object sender, RoutedEventArgs args) => ResumeRequested?.Invoke(this, ((Button)sender).Tag?.ToString() ?? string.Empty);
    private void RemoveClicked(object sender, RoutedEventArgs args) => RemoveRequested?.Invoke(this, ((Button)sender).Tag?.ToString() ?? string.Empty);
}
