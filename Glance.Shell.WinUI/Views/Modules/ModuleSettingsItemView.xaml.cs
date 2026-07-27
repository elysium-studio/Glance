using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.Foundation;

namespace Glance.Shell.WinUI;

public sealed partial class ModuleSettingsItemView :
    UserControl
{
    private const int ReorderHoldDurationMs = 600;
    private const double ReorderHoldMovementThreshold = 8;

    private DispatcherQueueTimer? reorderHoldTimer;
    private uint? pressedPointerId;
    private Point pressedPosition;

    public ModuleSettingsItemView()
    {
        InitializeComponent();

        Unloaded += HandleUnloaded;
        AddHandler(PointerPressedEvent, new PointerEventHandler(HandlePointerPressed), true);
        AddHandler(PointerMovedEvent, new PointerEventHandler(HandlePointerMoved), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler(HandlePointerReleased), true);
        AddHandler(PointerCanceledEvent, new PointerEventHandler(HandlePointerCanceled), true);
        AddHandler(PointerCaptureLostEvent, new PointerEventHandler(HandlePointerCaptureLost), true);
        AddHandler(PointerExitedEvent, new PointerEventHandler(HandlePointerExited), true);
    }

    public ModuleSettingsItemViewModel ViewModel => (ModuleSettingsItemViewModel)DataContext;

    private void HandlePointerPressed(object sender,
        PointerRoutedEventArgs args)
    {
        PointerPoint point = args.GetCurrentPoint(this);

        if (!point.Properties.IsLeftButtonPressed ||
            DataContext is not ModuleSettingsItemViewModel viewModel ||
            viewModel.IsReordering ||
            IsToggleInteraction(args.OriginalSource))
        {
            return;
        }

        pressedPointerId = args.Pointer.PointerId;
        pressedPosition = point.Position;
        reorderHoldTimer ??= CreateReorderHoldTimer();
        reorderHoldTimer.Stop();
        reorderHoldTimer.Start();
    }

    private void HandlePointerMoved(object sender,
        PointerRoutedEventArgs args)
    {
        if (pressedPointerId != args.Pointer.PointerId)
        {
            return;
        }

        Point position = args.GetCurrentPoint(this).Position;
        double horizontalDistance = position.X - pressedPosition.X;
        double verticalDistance = position.Y - pressedPosition.Y;
        double distanceSquared = (horizontalDistance * horizontalDistance) +
            (verticalDistance * verticalDistance);

        if (distanceSquared > ReorderHoldMovementThreshold * ReorderHoldMovementThreshold)
        {
            CancelReorderHold();
        }
    }

    private void HandlePointerReleased(object sender,
        PointerRoutedEventArgs args) =>
        CancelReorderHold(args.Pointer.PointerId);

    private void HandlePointerCanceled(object sender,
        PointerRoutedEventArgs args) =>
        CancelReorderHold(args.Pointer.PointerId);

    private void HandlePointerCaptureLost(object sender,
        PointerRoutedEventArgs args) =>
        CancelReorderHold(args.Pointer.PointerId);

    private void HandlePointerExited(object sender,
        PointerRoutedEventArgs args)
    {
        Point position = args.GetCurrentPoint(this).Position;

        if (position.X < 0 ||
            position.Y < 0 ||
            position.X > ActualWidth ||
            position.Y > ActualHeight)
        {
            CancelReorderHold(args.Pointer.PointerId);
        }
    }

    private void HandleUnloaded(object sender,
        RoutedEventArgs args) =>
        CancelReorderHold();

    private DispatcherQueueTimer CreateReorderHoldTimer()
    {
        DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(ReorderHoldDurationMs);
        timer.IsRepeating = false;
        timer.Tick += HandleReorderHoldTimerTick;
        return timer;
    }

    private void HandleReorderHoldTimerTick(DispatcherQueueTimer sender,
        object args)
    {
        sender.Stop();
        pressedPointerId = null;

        if (DataContext is ModuleSettingsItemViewModel viewModel)
        {
            viewModel.RequestReordering();
        }
    }

    private void CancelReorderHold(uint pointerId)
    {
        if (pressedPointerId == pointerId)
        {
            CancelReorderHold();
        }
    }

    private void CancelReorderHold()
    {
        reorderHoldTimer?.Stop();
        pressedPointerId = null;
    }

    private bool IsToggleInteraction(object source)
    {
        DependencyObject? current = source as DependencyObject;

        while (current is not null &&
            !ReferenceEquals(current, this))
        {
            if (current is ToggleSwitch)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
