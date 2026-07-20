using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Clipboard.WinUI;

public sealed partial class ClipboardExpandedView : UserControl
{
    public ClipboardExpandedView(ClipboardShelfViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public ClipboardShelfViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private async void HandleCopyClick(object sender, RoutedEventArgs args)
    {
        if (GetEntry(sender) is ClipboardEntry entry)
        {
            await ViewModel.CopyAsync(entry);
        }
    }

    private async void HandlePasteClick(object sender, RoutedEventArgs args)
    {
        if (GetEntry(sender) is ClipboardEntry entry)
        {
            await ViewModel.PasteAsync(entry);
        }
    }

    private async void HandleRemoveClick(object sender, RoutedEventArgs args)
    {
        if (GetEntry(sender) is ClipboardEntry entry)
        {
            await ViewModel.RemoveAsync(entry);
        }
    }

    private static ClipboardEntry? GetEntry(object sender) =>
        (sender as FrameworkElement)?.DataContext as ClipboardEntry;
}
