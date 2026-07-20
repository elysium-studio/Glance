using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Glance.DropShelf.WinUI;

public sealed partial class DropShelfExpandedView : UserControl
{
    public DropShelfExpandedView(DropShelfViewModel viewModel)
    {
        ViewModel = viewModel;
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
            ViewModel.Remove(item);
        }
    }

    private async void HandleDragStarting(UIElement sender, DragStartingEventArgs args)
    {
        var deferral = args.GetDeferral();

        try
        {
            List<IStorageItem> storageItems = [];

            foreach (DropShelfItem item in ViewModel.Items)
            {
                try
                {
                    IStorageItem storageItem = item.IsFolder
                        ? await StorageFolder.GetFolderFromPathAsync(item.Path)
                        : await StorageFile.GetFileFromPathAsync(item.Path);
                    storageItems.Add(storageItem);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine(
                        $"DropShelf: cannot add '{item.Path}' to outgoing drag: {exception}");
                }
            }

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
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"DropShelf: failed to start outgoing drag: {exception}");
            args.Cancel = true;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void HandleDropCompleted(UIElement sender, DropCompletedEventArgs args)
    {
        if (args.DropResult == DataPackageOperation.Move)
        {
            ViewModel.Clear();
        }
    }
}
