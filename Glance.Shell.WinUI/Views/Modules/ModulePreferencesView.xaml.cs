using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace Glance.Shell.WinUI;

public sealed partial class ModulePreferencesView :
    UserControl
{
    private const double ReorderThreshold = 6;
    private ModuleSettingsItemViewModel? draggedModule;
    private IAsyncOperation<DataPackageOperation>? reorderOperation;
    private ListViewItem? reorderCandidate;
    private Point reorderStartPoint;

    public ModulePreferencesView()
    {
        InitializeComponent();
        ModulesListView.AddHandler(PointerPressedEvent, new PointerEventHandler(HandleModulePointerPressed), true);
        ModulesListView.AddHandler(PointerMovedEvent, new PointerEventHandler(HandleModulePointerMoved), true);
        ModulesListView.AddHandler(PointerReleasedEvent, new PointerEventHandler(HandleModulePointerReleased), true);
        ModulesListView.AddHandler(PointerCanceledEvent, new PointerEventHandler(HandleModulePointerReleased), true);
    }

    public ModulePreferencesViewModel ViewModel =>
        (ModulePreferencesViewModel)DataContext;

    public static Visibility WhenSettingsAvailable(bool hasSettings) =>
        hasSettings ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility WhenSettingsUnavailable(bool hasSettings) =>
        hasSettings ? Visibility.Collapsed : Visibility.Visible;

    private void HandleModulePointerPressed(object sender,
        PointerRoutedEventArgs args)
    {
        PointerPoint point = args.GetCurrentPoint(ModulesListView);

        if (!point.Properties.IsLeftButtonPressed ||
            args.OriginalSource is not DependencyObject source ||
            FindListViewItem(source) is not ListViewItem container ||
            IsExpandedSettingContent(source, container))
        {
            ResetReorderCandidate();
            return;
        }

        reorderCandidate = container;
        reorderStartPoint = point.Position;
    }

    private void HandleModulePointerMoved(object sender,
        PointerRoutedEventArgs args)
    {
        if (reorderCandidate is not ListViewItem container ||
            reorderOperation is not null)
        {
            return;
        }

        PointerPoint listPoint = args.GetCurrentPoint(ModulesListView);

        if (!listPoint.Properties.IsLeftButtonPressed)
        {
            ResetReorderCandidate();
            return;
        }

        double horizontalDistance = Math.Abs(listPoint.Position.X - reorderStartPoint.X);
        double verticalDistance = Math.Abs(listPoint.Position.Y - reorderStartPoint.Y);

        if (horizontalDistance < ReorderThreshold &&
            verticalDistance < ReorderThreshold)
        {
            return;
        }

        _ = container.CapturePointer(args.Pointer);
        draggedModule = (ModuleSettingsItemViewModel)ModulesListView.ItemFromContainer(container);
        container.DragStarting += HandleModuleDragStarting;
        reorderOperation = container.StartDragAsync(args.GetCurrentPoint(container));
        reorderOperation.Completed = HandleReorderOperationCompleted;
        args.Handled = true;
    }

    private void HandleModulePointerReleased(object sender,
        PointerRoutedEventArgs args)
    {
        if (reorderOperation is null)
        {
            ResetReorderCandidate();
        }
    }

    private void HandleModuleDragStarting(UIElement sender,
        DragStartingEventArgs args)
    {
        args.Data.RequestedOperation = DataPackageOperation.Move;
        args.Data.SetText(draggedModule?.Id ?? string.Empty);
    }

    private void HandleModuleDragOver(object sender,
        DragEventArgs args)
    {
        if (draggedModule is null)
        {
            return;
        }

        args.AcceptedOperation = DataPackageOperation.Move;
        args.DragUIOverride.IsCaptionVisible = false;
        args.Handled = true;
    }

    private async void HandleModuleDrop(object sender,
        DragEventArgs args)
    {
        if (draggedModule is null)
        {
            return;
        }

        int oldIndex = ViewModel.Modules.IndexOf(draggedModule);
        int insertionIndex = FindInsertionIndex(args.GetPosition(ModulesListView));

        if (insertionIndex > oldIndex)
        {
            insertionIndex--;
        }

        int newIndex = Math.Clamp(insertionIndex, 0, ViewModel.Modules.Count - 1);

        if (oldIndex >= 0 &&
            oldIndex != newIndex)
        {
            ViewModel.Modules.Move(oldIndex, newIndex);
            await ViewModel.SaveOrderAsync();
        }

        args.AcceptedOperation = DataPackageOperation.Move;
        args.Handled = true;
    }

    private void HandleReorderOperationCompleted(IAsyncOperation<DataPackageOperation> operation,
        AsyncStatus status) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            if (reorderCandidate is not null)
            {
                reorderCandidate.DragStarting -= HandleModuleDragStarting;
            }

            reorderCandidate?.ReleasePointerCaptures();
            reorderOperation = null;
            draggedModule = null;
            ResetReorderCandidate();
        });

    private int FindInsertionIndex(Point pointerPosition)
    {
        int lastRealizedIndex = -1;

        for (int index = 0; index < ViewModel.Modules.Count; index++)
        {
            if (ModulesListView.ContainerFromIndex(index) is not ListViewItem container)
            {
                continue;
            }

            lastRealizedIndex = index;
            Rect bounds = container.TransformToVisual(ModulesListView)
                .TransformBounds(new Rect(0, 0, container.ActualWidth, container.ActualHeight));

            if (pointerPosition.Y < bounds.Y + (bounds.Height / 2))
            {
                return index;
            }
        }

        return lastRealizedIndex + 1;
    }

    private ListViewItem? FindListViewItem(DependencyObject source)
    {
        DependencyObject? current = source;

        while (current is not null &&
            !ReferenceEquals(current, ModulesListView))
        {
            if (current is ListViewItem container)
            {
                return container;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static bool IsExpandedSettingContent(DependencyObject source,
        ListViewItem container)
    {
        DependencyObject? current = source;
        bool insideSettingsCard = false;

        while (current is not null &&
            !ReferenceEquals(current, container))
        {
            if (current is SettingsCard)
            {
                insideSettingsCard = true;
            }
            else if (current is SettingsExpander)
            {
                return insideSettingsCard;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void ResetReorderCandidate() =>
        reorderCandidate = null;
}
