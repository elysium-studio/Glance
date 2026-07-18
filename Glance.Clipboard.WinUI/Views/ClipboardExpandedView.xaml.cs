using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Clipboard.WinUI;

public sealed partial class ClipboardExpandedView : UserControl
{
    public ClipboardExpandedView(ClipboardShelfViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private ClipboardShelfViewModel ViewModel =>
        (ClipboardShelfViewModel)DataContext;

    private async void HandleItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is ClipboardEntry entry)
        {
            await ViewModel.RestoreAsync(entry);
        }
    }
}
