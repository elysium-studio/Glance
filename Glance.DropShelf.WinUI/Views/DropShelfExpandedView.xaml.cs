using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Glance.DropShelf.WinUI;

public sealed partial class DropShelfExpandedView : UserControl
{
    private const int MoveConfirmationAttempts = 240;
    private static readonly TimeSpan MoveConfirmationInterval = TimeSpan.FromMilliseconds(250);

    private readonly DispatcherQueue dispatcherQueue;
    private readonly DropShelfTransferStore transferStore;
    private CancellationTokenSource? moveConfirmation;
    private string[] outgoingPaths = [];

    public DropShelfExpandedView(
        DropShelfViewModel viewModel,
        DropShelfTransferStore transferStore)
    {
        ViewModel = viewModel;
        this.transferStore = transferStore;
        InitializeComponent();
        dispatcherQueue = DispatcherQueue;
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

            CancelMoveConfirmation();
            outgoingPaths = storageItems
                .Select(item => item.Path)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray();
            args.AllowedOperations = DataPackageOperation.Move;
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

    private void HandleDropCompleted(UIElement sender, DropCompletedEventArgs args)
    {
        Debug.WriteLine($"DropShelf: outgoing drag completed with {args.DropResult}.");
        string[] paths = outgoingPaths;
        outgoingPaths = [];

        if (args.DropResult != DataPackageOperation.None)
        {
            QueueRemoveOutgoingItems(paths);
            return;
        }

        moveConfirmation = new CancellationTokenSource();
        _ = ConfirmMoveAsync(paths, moveConfirmation.Token);
    }

    private async Task ConfirmMoveAsync(
        IReadOnlyCollection<string> paths,
        CancellationToken cancellationToken)
    {
        if (paths.Count == 0)
        {
            return;
        }

        Debug.WriteLine(
            $"DropShelf: monitoring {paths.Count} source path(s) for an asynchronous Explorer move.");

        try
        {
            for (int attempt = 0; attempt < MoveConfirmationAttempts; attempt++)
            {
                if (paths.All(IsMissing))
                {
                    Debug.WriteLine("DropShelf: confirmed outgoing move from source paths.");
                    QueueRemoveOutgoingItems(paths);
                    return;
                }

                await Task.Delay(MoveConfirmationInterval, cancellationToken)
                    .ConfigureAwait(false);
            }

            Debug.WriteLine(
                "DropShelf: outgoing move was not confirmed; preserving remaining shelf items.");
            QueueRemoveMissingOutgoingItems(paths);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static bool IsMissing(string path) =>
        !File.Exists(path) && !Directory.Exists(path);

    private void QueueRemoveOutgoingItems(IReadOnlyCollection<string> paths) =>
        Queue(() => RemoveOutgoingItems(paths));

    private void QueueRemoveMissingOutgoingItems(IReadOnlyCollection<string> paths) =>
        Queue(() => RemoveOutgoingItems(paths.Where(IsMissing).ToArray()));

    private void Queue(Action action)
    {
        if (!dispatcherQueue.TryEnqueue(() => action()))
        {
            Debug.WriteLine("DropShelf: could not queue outgoing transfer cleanup.");
        }
    }

    private void RemoveOutgoingItems(IReadOnlyCollection<string> paths)
    {
        HashSet<string> transferredPaths = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (DropShelfItem item in ViewModel.Items.ToArray())
        {
            if (!transferredPaths.Contains(item.Path))
            {
                continue;
            }

            transferStore.Remove(item.Path);
            ViewModel.Remove(item);
        }
    }

    private void CancelMoveConfirmation()
    {
        moveConfirmation?.Cancel();
        moveConfirmation?.Dispose();
        moveConfirmation = null;
    }
}
