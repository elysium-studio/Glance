using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.ApplicationModel.DataTransfer;

namespace Glance.Stash.WinUI;

public sealed partial class StashExpandedView :
    UserControl
{
    public StashExpandedView(StashViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public StashViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private string ToUpper(string value) => value.ToUpperInvariant();

    private Visibility WhenEmpty(bool hasItems) => hasItems ? Visibility.Collapsed : Visibility.Visible;

    private Visibility WhenPopulated(bool hasItems) => hasItems ? Visibility.Visible : Visibility.Collapsed;

    private void HandleItemDragStarting(UIElement sender,
        DragStartingEventArgs args)
    {
        if ((sender as FrameworkElement)?.DataContext is not StashItem item)
        {
            return;
        }

        args.Data.SetText(item.Content);

        if (item.Kind == StashItemKind.Link &&
            Uri.TryCreate(item.Content, UriKind.Absolute, out Uri? uri))
        {
            args.Data.SetWebLink(uri);
        }

        args.Data.RequestedOperation = DataPackageOperation.Copy;
    }
}
