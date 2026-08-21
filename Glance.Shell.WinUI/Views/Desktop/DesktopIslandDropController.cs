using Elysium.UI.Controls.WinUI;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace Glance.Shell.WinUI;

internal sealed class DesktopIslandDropController :
    IDesktopIslandDropController
{
    private const int ContextualDragExitDelayMs = 160;
    private const double RouteScrollEdgeWidth = 48;
    private const double RouteScrollMaximumVelocity = 560;
    private const double RouteScrollMinimumVelocity = 120;

    private readonly IDesktopIslandContentReader contentReader;
    private DispatcherQueueTimer? contextualDragExitTimer;
    private FrameworkElement? activeContentRouteTarget;
    private IDesktopIslandDropHost? host;
    private string? droppedContentRouteId;
    private bool isContextualDragActive;
    private bool isRouteScrollActive;
    private int contextualDragSession;
    private long routeScrollTimestamp;
    private double routeScrollVelocity;

    public DesktopIslandDropController(IDesktopIslandContentReader contentReader) => this.contentReader = contentReader;

    public bool IsActive => isContextualDragActive;

    public void Attach(IDesktopIslandDropHost host) => this.host = host;

    public void Detach()
    {
        StopContextualDragExitTimer();
        StopRouteAutoScroll();
        ReleaseActiveContentRouteTarget();

        contextualDragExitTimer = null;
        droppedContentRouteId = null;
        isContextualDragActive = false;
        contextualDragSession++;
        host = null;
    }

    public async Task EnterAsync(DragEventArgs args)
    {
        IDesktopIslandDropHost currentHost = GetHost();

        if (currentHost.IsModuleReorderVisible)
        {
            return;
        }

        if (!CanHandleContent(args.DataView))
        {
            StopRouteAutoScroll();
            args.AcceptedOperation = DataPackageOperation.None;
            ScheduleContextualDragExit();
            return;
        }

        StopContextualDragExitTimer();
        isContextualDragActive = true;
        int session = ++contextualDragSession;
        args.AcceptedOperation = DataPackageOperation.Copy;
        DragOperationDeferral deferral = args.GetDeferral();
        GlanceContentContext? context = null;

        try
        {
            context = await ReadContentAsync(args.DataView);
        }
        catch (COMException)
        {
        }
        catch (Exception)
        {
        }
        finally
        {
            await CompleteDropDeferralAsync(deferral);
        }

        if (context is null)
        {
            return;
        }

        _ = currentHost.DispatcherQueue.TryEnqueue(() =>
        {
            if (isContextualDragActive && session == contextualDragSession)
            {
                bool restoreExpandedState = currentHost.HostMode == DesktopIslandHostMode.Taskbar ? currentHost.IsPinned : currentHost.IsExpanded;
                _ = currentHost.TryActivateContent(context, restoreExpandedState);
            }
        });
    }

    public void Over(DragEventArgs args)
    {
        if (GetHost().IsModuleReorderVisible)
        {
            return;
        }

        if (!CanHandleContent(args.DataView))
        {
            StopRouteAutoScroll();
            args.AcceptedOperation = DataPackageOperation.None;
            ScheduleContextualDragExit();
            return;
        }

        StopContextualDragExitTimer();
        args.AcceptedOperation = DataPackageOperation.Copy;
    }

    public void Leave()
    {
        StopRouteAutoScroll();

        if (!GetHost().IsModuleReorderVisible)
        {
            ScheduleContextualDragExit();
        }
    }

    public void EnterRoute(object sender, DragEventArgs args)
    {
        if (sender is not FrameworkElement element || element.DataContext is not GlanceContentRoute)
        {
            args.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        StopContextualDragExitTimer();
        SetActiveContentRouteTarget(element);
        UpdateRouteAutoScroll(args);
        args.AcceptedOperation = DataPackageOperation.Copy;
        args.Handled = true;
    }

    public void OverRoute(DragEventArgs args)
    {
        StopContextualDragExitTimer();
        UpdateRouteAutoScroll(args);
        args.AcceptedOperation = DataPackageOperation.Copy;
        args.Handled = true;
    }

    public void OverRoutePicker(DragEventArgs args)
    {
        StopContextualDragExitTimer();
        UpdateRouteAutoScroll(args);
        args.AcceptedOperation = DataPackageOperation.Copy;
    }

    public void LeaveRoute(object sender)
    {
        if (ReferenceEquals(sender, activeContentRouteTarget))
        {
            ReleaseActiveContentRouteTarget();
        }

        ScheduleContextualDragExit();
    }

    public void LeaveRoutePicker() => StopRouteAutoScroll();

    public void DropOnRoute(object sender, DragEventArgs args)
    {
        if (sender is not FrameworkElement element || element.DataContext is not GlanceContentRoute route)
        {
            args.AcceptedOperation = DataPackageOperation.None;
            args.Handled = true;
            return;
        }

        droppedContentRouteId = route.Id;
        ReleaseActiveContentRouteTarget();
        StopContextualDragExitTimer();
        StopRouteAutoScroll();
        args.AcceptedOperation = DataPackageOperation.Copy;
    }

    public void ReleaseActiveRouteTarget()
    {
        StopRouteAutoScroll();
        ReleaseActiveContentRouteTarget();
    }

    public void ResetRoutePicker()
    {
        IDesktopIslandDropHost currentHost = GetHost();
        StopRouteAutoScroll();
        _ = currentHost.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (ReferenceEquals(host, currentHost))
            {
                _ = currentHost.ContentRouteScrollViewer.ChangeView(0, null, null, true);
            }
        });
    }

    public async Task DropAsync(DragEventArgs args)
    {
        IDesktopIslandDropHost currentHost = GetHost();

        if (currentHost.IsModuleReorderVisible)
        {
            return;
        }

        StopContextualDragExitTimer();
        StopRouteAutoScroll();
        string? routeId = droppedContentRouteId;
        droppedContentRouteId = null;

        if (routeId is not null && !currentHost.TryActivateContentRoute(routeId))
        {
            CompleteContextualDrag(false);
            return;
        }

        DragOperationDeferral deferral = args.GetDeferral();
        GlanceContentContext? context = null;
        bool contentHandled = false;

        try
        {
            context = await ReadContentAsync(args.DataView);
        }
        catch (COMException)
        {
        }
        catch (Exception)
        {
        }
        finally
        {
            await CompleteDropDeferralAsync(deferral);
        }

        if (context is not null)
        {
            try
            {
                contentHandled = await ProcessContentAsync(context);
            }
            catch (Exception)
            {
            }
        }

        CompleteContextualDrag(contentHandled);
    }

    private IDesktopIslandDropHost GetHost() => host ?? throw new InvalidOperationException("The drop controller is not attached.");

    private bool CanHandleContent(DataPackageView dataView) => contentReader.GetAvailableKinds(dataView).Any(GetHost().CanHandleContent);

    private async Task<GlanceContentContext?> ReadContentAsync(DataPackageView dataView)
    {
        IDesktopIslandDropHost currentHost = GetHost();

        foreach (GlanceContentKind kind in contentReader.GetAvailableKinds(dataView))
        {
            if (!currentHost.CanHandleContent(kind))
            {
                continue;
            }

            GlanceContentContext? context = await contentReader.ReadAsync(dataView, kind);

            if (context is not null)
            {
                return context;
            }
        }

        return null;
    }

    private void SetActiveContentRouteTarget(FrameworkElement target)
    {
        if (ReferenceEquals(activeContentRouteTarget, target))
        {
            return;
        }

        ReleaseActiveContentRouteTarget();
        activeContentRouteTarget = target;
        Canvas.SetZIndex(target, 1);
        FluentMotion.PlayRouteTargetHover(target);
    }

    private void ReleaseActiveContentRouteTarget()
    {
        if (activeContentRouteTarget is null)
        {
            return;
        }

        FrameworkElement target = activeContentRouteTarget;
        activeContentRouteTarget = null;
        Canvas.SetZIndex(target, 0);
        FluentMotion.PlayRouteTargetRelease(target);
    }

    private void ScheduleContextualDragExit()
    {
        if (!isContextualDragActive)
        {
            return;
        }

        contextualDragExitTimer ??= CreateContextualDragExitTimer();
        contextualDragExitTimer.Stop();
        contextualDragExitTimer.Start();
    }

    private DispatcherQueueTimer CreateContextualDragExitTimer()
    {
        DispatcherQueueTimer timer = GetHost().DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(ContextualDragExitDelayMs);
        timer.IsRepeating = false;
        timer.Tick += HandleContextualDragExitTimerTick;
        return timer;
    }

    private void StopContextualDragExitTimer() => contextualDragExitTimer?.Stop();

    private void UpdateRouteAutoScroll(DragEventArgs args)
    {
        ScrollViewer scrollViewer = GetHost().ContentRouteScrollViewer;

        if (scrollViewer.ActualWidth <= 0 || scrollViewer.ScrollableWidth <= 0)
        {
            StopRouteAutoScroll();
            return;
        }

        double edgeWidth = Math.Min(RouteScrollEdgeWidth, scrollViewer.ActualWidth / 2);
        double pointerX = args.GetPosition(scrollViewer).X;
        double direction = pointerX < edgeWidth && scrollViewer.HorizontalOffset > 0 ? -1 : pointerX > scrollViewer.ActualWidth - edgeWidth && scrollViewer.HorizontalOffset < scrollViewer.ScrollableWidth ? 1 : 0;

        if (direction == 0)
        {
            StopRouteAutoScroll();
            return;
        }

        double edgeProgress = direction < 0 ? 1 - Math.Clamp(pointerX / edgeWidth, 0, 1) : 1 - Math.Clamp((scrollViewer.ActualWidth - pointerX) / edgeWidth, 0, 1);
        routeScrollVelocity = direction * (RouteScrollMinimumVelocity + ((RouteScrollMaximumVelocity - RouteScrollMinimumVelocity) * edgeProgress));

        if (!isRouteScrollActive)
        {
            routeScrollTimestamp = Environment.TickCount64;
            CompositionTarget.Rendering += HandleRouteScrollRendering;
            isRouteScrollActive = true;
        }
    }

    private void StopRouteAutoScroll()
    {
        if (isRouteScrollActive)
        {
            CompositionTarget.Rendering -= HandleRouteScrollRendering;
        }

        isRouteScrollActive = false;
        routeScrollTimestamp = 0;
        routeScrollVelocity = 0;
    }

    private void HandleRouteScrollRendering(object? sender, object args)
    {
        if (host is null)
        {
            StopRouteAutoScroll();
            return;
        }

        ScrollViewer scrollViewer = host.ContentRouteScrollViewer;
        long timestamp = Environment.TickCount64;
        double elapsed = Math.Min((timestamp - routeScrollTimestamp) / 1000d, 0.05);
        double offset = Math.Clamp(scrollViewer.HorizontalOffset + (routeScrollVelocity * elapsed), 0, scrollViewer.ScrollableWidth);
        routeScrollTimestamp = timestamp;
        _ = scrollViewer.ChangeView(offset, null, null, true);

        if (offset <= 0 || offset >= scrollViewer.ScrollableWidth)
        {
            StopRouteAutoScroll();
        }
    }

    private void HandleContextualDragExitTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        CompleteContextualDrag(false);
    }

    private void CompleteContextualDrag(bool contentHandled)
    {
        IDesktopIslandDropHost currentHost = GetHost();

        if (!currentHost.DispatcherQueue.HasThreadAccess)
        {
            _ = currentHost.DispatcherQueue.TryEnqueue(() => CompleteContextualDrag(contentHandled));
            return;
        }

        StopContextualDragExitTimer();
        StopRouteAutoScroll();
        isContextualDragActive = false;
        droppedContentRouteId = null;
        contextualDragSession++;

        if (contentHandled)
        {
            currentHost.CompleteContentDrop();
            return;
        }

        currentHost.CancelContentDrop();
    }

    private Task CompleteDropDeferralAsync(DragOperationDeferral deferral)
    {
        DispatcherQueue dispatcherQueue = GetHost().DispatcherQueue;

        if (dispatcherQueue.HasThreadAccess)
        {
            CompleteDropDeferral(deferral);
            return Task.CompletedTask;
        }

        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(() =>
        {
            CompleteDropDeferral(deferral);
            _ = completion.TrySetResult(true);
        }))
        {
            _ = completion.TrySetResult(false);
        }

        return completion.Task;
    }

    private static void CompleteDropDeferral(DragOperationDeferral deferral)
    {
        try
        {
            deferral.Complete();
        }
        catch (COMException)
        {
        }
    }

    private Task<bool> ProcessContentAsync(GlanceContentContext context)
    {
        IDesktopIslandDropHost currentHost = GetHost();

        if (currentHost.DispatcherQueue.HasThreadAccess)
        {
            return currentHost.HandleContentAsync(context);
        }

        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!currentHost.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                _ = completion.TrySetResult(await currentHost.HandleContentAsync(context));
            }
            catch (Exception exception)
            {
                _ = completion.TrySetException(exception);
            }
        }))
        {
            _ = completion.TrySetResult(false);
        }

        return completion.Task;
    }
}
