using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;

namespace Glance.DropShelf.WinUI;

public sealed partial class DropShelfExpandedView : UserControl
{
    private const double DragThreshold = 4;

    private readonly DropShelfTransferStore transferStore;
    private Point dragStartPosition;
    private uint? dragPointerId;
    private bool isStartingDrag;

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
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"DropShelf: failed to start outgoing drag: {exception}");
            args.Cancel = true;
        }
    }

    private void HandleDragPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (sender is not UIElement source || !ViewModel.HasItems || isStartingDrag)
        {
            return;
        }

        dragPointerId = args.Pointer.PointerId;
        dragStartPosition = args.GetCurrentPoint(source).Position;
        source.CapturePointer(args.Pointer);
    }

    private async void HandleDragPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (sender is not UIElement source ||
            isStartingDrag ||
            dragPointerId != args.Pointer.PointerId)
        {
            return;
        }

        var pointerPoint = args.GetCurrentPoint(source);
        var horizontalDistance = Math.Abs(pointerPoint.Position.X - dragStartPosition.X);
        var verticalDistance = Math.Abs(pointerPoint.Position.Y - dragStartPosition.Y);

        if (horizontalDistance < DragThreshold && verticalDistance < DragThreshold)
        {
            return;
        }

        isStartingDrag = true;
        dragPointerId = null;
        source.ReleasePointerCapture(args.Pointer);

        try
        {
            var result = await source.StartDragAsync(pointerPoint);

            if (result != DataPackageOperation.None)
            {
                ClearShelfOnUiThread();
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"DropShelf: outgoing drag failed: {exception}");
        }
        finally
        {
            isStartingDrag = false;
        }
    }

    private void HandleDragPointerEnded(object sender, PointerRoutedEventArgs args)
    {
        if (isStartingDrag || dragPointerId != args.Pointer.PointerId)
        {
            return;
        }

        dragPointerId = null;

        if (sender is UIElement source)
        {
            source.ReleasePointerCapture(args.Pointer);
        }
    }

    private void ClearShelfOnUiThread()
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            ClearShelf();
            return;
        }

        DispatcherQueue.TryEnqueue(ClearShelf);
    }

    private void ClearShelf()
    {
        transferStore.Clear();
        ViewModel.Clear();
    }
}
