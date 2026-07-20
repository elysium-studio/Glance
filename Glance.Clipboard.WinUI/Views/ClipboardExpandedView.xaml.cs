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

    private string ToUpper(string value) => value.ToUpperInvariant();

    private async void HandleItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is ClipboardEntry entry)
        {
            await ViewModel.RestoreAsync(entry);
        }
    }
}
