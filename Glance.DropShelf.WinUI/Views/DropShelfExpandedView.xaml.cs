using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Glance.DropShelf.WinUI;

public sealed partial class DropShelfExpandedView : UserControl
{
    private readonly DropShelfTransferStore transferStore;

    public DropShelfExpandedView(
        DropShelfViewModel viewModel,
        DropShelfTransferStore transferStore)
    {
        ViewModel = viewModel;
        this.transferStore = transferStore;
        InitializeComponent();
    }

    public DropShelfViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private string ToUpper(string value) => value.ToUpperInvariant();

    private Visibility WhenEmpty(bool hasItems) =>
        hasItems ? Visibility.Collapsed : Visibility.Visible;

    private Visibility WhenPopulated(bool hasItems) =>
        hasItems ? Visibility.Visible : Visibility.Collapsed;

    private void HandleRemoveItemClick(object sender, RoutedEventArgs args)
    {
        if ((sender as FrameworkElement)?.DataContext is DropShelfItem item)
        {
            transferStore.Remove(item.Path);
            ViewModel.Remove(item);
        }
    }

    private void HandleDragStarting(UIElement sender, DragStartingEventArgs args)
    {
        try
        {
            IReadOnlyList<IStorageItem> storageItems =
                transferStore.GetStorageItems(ViewModel.Items);

            if (storageItems.Count == 0)
            {
                Debug.WriteLine("DropShelf: outgoing drag contains no accessible items.");
                args.Cancel = true;
                ViewModel.RemoveMissingItems();
                return;
            }

            args.Data.SetStorageItems(storageItems, false);
            args.Data.RequestedOperation = DataPackageOperation.Move;
            args.Data.Properties.Title = ViewModel.DragCaption;
            DispatcherQueue.TryEnqueue(ClearShelf);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"DropShelf: failed to start outgoing drag: {exception}");
            args.Cancel = true;
        }
    }

    private void HandleDropCompleted(UIElement sender, DropCompletedEventArgs args)
    {
        if (args.DropResult != DataPackageOperation.None)
        {
            ClearShelf();
        }
    }

    private void ClearShelf()
    {
        transferStore.Clear();
        ViewModel.Clear();
    }
}
