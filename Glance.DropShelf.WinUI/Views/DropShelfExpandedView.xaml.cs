using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
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
                catch (Exception)
                {
                    // The item may have moved or been deleted since it was staged.
                }
            }

            if (storageItems.Count == 0)
            {
                args.Cancel = true;
                ViewModel.RemoveMissingItems();
                return;
            }

            args.Data.SetStorageItems(storageItems);
            args.Data.RequestedOperation = DataPackageOperation.Move;
            args.Data.Properties.Title = ViewModel.DragCaption;
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
