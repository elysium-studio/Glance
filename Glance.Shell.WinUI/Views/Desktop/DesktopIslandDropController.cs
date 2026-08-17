using Elysium.UI.Controls.WinUI;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    private readonly IDesktopIslandContentReader contentReader;
    private DispatcherQueueTimer? contextualDragExitTimer;
    private FrameworkElement? activeContentRouteTarget;
    private IDesktopIslandDropHost? host;
    private string? droppedContentRouteId;
    private bool isContextualDragActive;
    private int contextualDragSession;

    public DesktopIslandDropController(IDesktopIslandContentReader contentReader) => this.contentReader = contentReader;

    public bool IsActive => isContextualDragActive;

    public void Attach(IDesktopIslandDropHost host) => this.host = host;

    public void Detach()
    {
        StopContextualDragExitTimer();
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
            args.AcceptedOperation = DataPackageOperation.None;
            ScheduleContextualDragExit();
            return;
        }

        StopContextualDragExitTimer();
        args.AcceptedOperation = DataPackageOperation.Copy;
    }

    public void Leave()
    {
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
        args.AcceptedOperation = DataPackageOperation.Copy;
        args.Handled = true;
    }

    public void OverRoute(DragEventArgs args)
    {
        StopContextualDragExitTimer();
        args.AcceptedOperation = DataPackageOperation.Copy;
        args.Handled = true;
    }

    public void LeaveRoute(object sender)
    {
        if (ReferenceEquals(sender, activeContentRouteTarget))
        {
            ReleaseActiveContentRouteTarget();
        }

        ScheduleContextualDragExit();
    }

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
        args.AcceptedOperation = DataPackageOperation.Copy;
    }

    public void ReleaseActiveRouteTarget() => ReleaseActiveContentRouteTarget();

    public async Task DropAsync(DragEventArgs args)
    {
        IDesktopIslandDropHost currentHost = GetHost();

        if (currentHost.IsModuleReorderVisible)
        {
            return;
        }

        StopContextualDragExitTimer();
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
