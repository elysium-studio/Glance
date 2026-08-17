using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Glance.Shell.WinUI;

internal sealed class DesktopIslandModuleReorderController :
    IDesktopIslandModuleReorderController
{
    private const float EdgeFadeWidth = 32;
    private const double SideItemMinimumOpacity = 0.68;

    private IDesktopIslandModuleReorderHost? host;
    private ListViewItem? draggedItem;
    private SoftwareBitmap? dragPreview;
    private ContainerVisual? edgeFadeContainer;
    private CompositionLinearGradientBrush? leftEdgeFadeGradient;
    private CompositionMaskBrush? leftEdgeFadeMask;
    private CompositionSurfaceBrush? leftEdgeFadeSourceBrush;
    private SpriteVisual? leftEdgeFadeVisual;
    private CompositionVisualSurface? leftEdgeSurface;
    private CompositionLinearGradientBrush? rightEdgeFadeGradient;
    private CompositionMaskBrush? rightEdgeFadeMask;
    private CompositionSurfaceBrush? rightEdgeFadeSourceBrush;
    private SpriteVisual? rightEdgeFadeVisual;
    private CompositionVisualSurface? rightEdgeSurface;
    private ScrollViewer? scrollViewer;
    private int centeredIndex = -1;
    private int targetIndex = -1;

    public void Attach(IDesktopIslandModuleReorderHost host)
    {
        this.host = host;
        host.ModuleReorderList.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(HandlePointerWheelChanged), true);
    }

    public void Detach()
    {
        if (host is not null)
        {
            host.ModuleReorderList.RemoveHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(HandlePointerWheelChanged));
        }

        if (scrollViewer is not null)
        {
            scrollViewer.ViewChanged -= HandleViewChanged;
        }

        dragPreview?.Dispose();
        dragPreview = null;
        draggedItem = null;
        scrollViewer = null;
        centeredIndex = -1;
        targetIndex = -1;
        DisposeEdgeFade();
        host = null;
    }

    public void CenterSelected()
    {
        IDesktopIslandModuleReorderHost currentHost = GetHost();
        IGlanceComponent? selectedComponent = currentHost.SelectedComponent;

        if (selectedComponent is null || currentHost.ModuleReorderList.ActualWidth <= 0)
        {
            return;
        }

        double edgePadding = Math.Max(0, (currentHost.ModuleReorderList.ActualWidth - currentHost.ModuleReorderItemWidth) / 2);
        currentHost.ModuleReorderList.Padding = new Thickness(edgePadding, 0, edgePadding, 0);
        currentHost.ModuleReorderList.UpdateLayout();
        CenterItem(currentHost.ModuleOrder.IndexOf(selectedComponent), true);
    }

    public void ListLoaded()
    {
        UpdateEdgeFade();
        ScrollViewer? currentScrollViewer = GetScrollViewer();

        if (currentScrollViewer is null)
        {
            _ = GetHost().DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => UpdateScrollButtons(GetScrollViewer()));
        }
    }

    public void EdgeFadeHostSizeChanged() => UpdateEdgeFade();

    public void PointerWheelChanged(PointerRoutedEventArgs args)
    {
        IDesktopIslandModuleReorderHost currentHost = GetHost();

        if (!currentHost.IsModuleReorderVisible)
        {
            return;
        }

        int delta = args.GetCurrentPoint(currentHost.ModuleReorderList).Properties.MouseWheelDelta;

        if (delta == 0)
        {
            return;
        }

        Scroll(delta < 0 ? 1 : -1);
        args.Handled = true;
    }

    public void Previous() => Scroll(-1);

    public void Next() => Scroll(1);

    public void ItemPointerEntered(object sender)
    {
        if (sender is FrameworkElement element && !ReferenceEquals(element, draggedItem))
        {
            FluentMotion.PlayRouteTargetHover(element);
        }
    }

    public void ItemPointerExited(object sender)
    {
        if (sender is FrameworkElement element && !ReferenceEquals(element, draggedItem))
        {
            FluentMotion.PlayRouteTargetRelease(element);
        }
    }

    public void DragStarting(DragItemsStartingEventArgs args)
    {
        IDesktopIslandModuleReorderHost currentHost = GetHost();
        object? item = args.Items.FirstOrDefault();
        draggedItem = item is null ? null : currentHost.ModuleReorderList.ContainerFromItem(item) as ListViewItem;

        if (draggedItem is not null)
        {
            Canvas.SetZIndex(draggedItem, 2);
            FluentMotion.PlayRouteTargetHover(draggedItem);
        }
    }

    public void DragCompleted()
    {
        ScrollViewer? currentScrollViewer = GetScrollViewer();
        dragPreview?.Dispose();
        dragPreview = null;

        if (draggedItem is null)
        {
            return;
        }

        ListViewItem item = draggedItem;
        draggedItem = null;
        Canvas.SetZIndex(item, 0);
        FluentMotion.PlayRouteTargetRelease(item);
        UpdateItemFade(currentScrollViewer);
    }

    public void DragOver(DragEventArgs args)
    {
        args.DragUIOverride.IsGlyphVisible = false;
        args.DragUIOverride.IsCaptionVisible = false;
        UpdateItemFade(scrollViewer);
    }

    public async Task CreateDragVisualAsync(DragStartingEventArgs args)
    {
        ListViewItem? item = FindVisualAncestor<ListViewItem>(args.OriginalSource as DependencyObject);

        if (item is null)
        {
            return;
        }

        DragOperationDeferral deferral = args.GetDeferral();

        try
        {
            RenderTargetBitmap renderer = new();
            await renderer.RenderAsync(item);

            if (renderer.PixelWidth <= 0 || renderer.PixelHeight <= 0)
            {
                return;
            }

            IBuffer pixels = await renderer.GetPixelsAsync();
            SoftwareBitmap preview = SoftwareBitmap.CreateCopyFromBuffer(pixels, BitmapPixelFormat.Bgra8, renderer.PixelWidth, renderer.PixelHeight, BitmapAlphaMode.Premultiplied);
            dragPreview?.Dispose();
            dragPreview = preview;
            args.DragUI.SetContentFromSoftwareBitmap(preview);
        }
        catch (Exception)
        {
            dragPreview?.Dispose();
            dragPreview = null;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private IDesktopIslandModuleReorderHost GetHost() => host ?? throw new InvalidOperationException("The module reorder controller is not attached.");

    private void HandlePointerWheelChanged(object sender, PointerRoutedEventArgs args) => PointerWheelChanged(args);

    private void UpdateEdgeFade()
    {
        IDesktopIslandModuleReorderHost currentHost = GetHost();
        float width = (float)currentHost.ModuleReorderEdgeFadeHost.ActualWidth;
        float height = (float)currentHost.ModuleReorderEdgeFadeHost.ActualHeight;

        if (!currentHost.IsLoaded || width <= 0 || height <= 0)
        {
            return;
        }

        float fadeWidth = Math.Min(EdgeFadeWidth, width / 2);
        Visual sourceVisual = ElementCompositionPreview.GetElementVisual(currentHost.ModuleReorderList);
        Compositor compositor = sourceVisual.Compositor;

        if (edgeFadeContainer is null)
        {
            CreateEdgeFade(compositor, sourceVisual);
        }

        Visual clipHostVisual = ElementCompositionPreview.GetElementVisual(currentHost.ModuleReorderListClipHost);
        InsetClip clip = compositor.CreateInsetClip();
        clip.LeftInset = fadeWidth;
        clip.RightInset = fadeWidth;
        clipHostVisual.Clip = clip;
        edgeFadeContainer!.Size = new Vector2(width, height);
        leftEdgeSurface!.SourceSize = new Vector2(fadeWidth, height);
        leftEdgeSurface.SourceOffset = Vector2.Zero;
        leftEdgeFadeVisual!.Size = new Vector2(fadeWidth, height);
        leftEdgeFadeVisual.Offset = Vector3.Zero;
        rightEdgeSurface!.SourceSize = new Vector2(fadeWidth, height);
        rightEdgeSurface.SourceOffset = new Vector2(width - fadeWidth, 0);
        rightEdgeFadeVisual!.Size = new Vector2(fadeWidth, height);
        rightEdgeFadeVisual.Offset = new Vector3(width - fadeWidth, 0, 0);
    }

    private void CreateEdgeFade(Compositor compositor, Visual sourceVisual)
    {
        IDesktopIslandModuleReorderHost currentHost = GetHost();
        leftEdgeSurface = compositor.CreateVisualSurface();
        leftEdgeSurface.SourceVisual = sourceVisual;
        leftEdgeFadeSourceBrush = compositor.CreateSurfaceBrush(leftEdgeSurface);
        leftEdgeFadeGradient = CreateEdgeFadeGradient(compositor, true);
        leftEdgeFadeMask = compositor.CreateMaskBrush();
        leftEdgeFadeMask.Source = leftEdgeFadeSourceBrush;
        leftEdgeFadeMask.Mask = leftEdgeFadeGradient;
        leftEdgeFadeVisual = compositor.CreateSpriteVisual();
        leftEdgeFadeVisual.Brush = leftEdgeFadeMask;
        rightEdgeSurface = compositor.CreateVisualSurface();
        rightEdgeSurface.SourceVisual = sourceVisual;
        rightEdgeFadeSourceBrush = compositor.CreateSurfaceBrush(rightEdgeSurface);
        rightEdgeFadeGradient = CreateEdgeFadeGradient(compositor, false);
        rightEdgeFadeMask = compositor.CreateMaskBrush();
        rightEdgeFadeMask.Source = rightEdgeFadeSourceBrush;
        rightEdgeFadeMask.Mask = rightEdgeFadeGradient;
        rightEdgeFadeVisual = compositor.CreateSpriteVisual();
        rightEdgeFadeVisual.Brush = rightEdgeFadeMask;
        edgeFadeContainer = compositor.CreateContainerVisual();
        edgeFadeContainer.Children.InsertAtTop(leftEdgeFadeVisual);
        edgeFadeContainer.Children.InsertAtTop(rightEdgeFadeVisual);
        ElementCompositionPreview.SetElementChildVisual(currentHost.ModuleReorderEdgeFadeHost, edgeFadeContainer);
    }

    private static CompositionLinearGradientBrush CreateEdgeFadeGradient(Compositor compositor, bool isLeftEdge)
    {
        CompositionLinearGradientBrush gradient = compositor.CreateLinearGradientBrush();
        gradient.StartPoint = Vector2.Zero;
        gradient.EndPoint = Vector2.UnitX;
        gradient.ColorStops.Add(compositor.CreateColorGradientStop(0, isLeftEdge ? Colors.Transparent : Colors.White));
        gradient.ColorStops.Add(compositor.CreateColorGradientStop(1, isLeftEdge ? Colors.White : Colors.Transparent));
        return gradient;
    }

    private void DisposeEdgeFade()
    {
        if (host is not null)
        {
            ElementCompositionPreview.GetElementVisual(host.ModuleReorderListClipHost).Clip = null;
            ElementCompositionPreview.SetElementChildVisual(host.ModuleReorderEdgeFadeHost, null);
        }

        leftEdgeFadeGradient?.Dispose();
        rightEdgeFadeGradient?.Dispose();
        leftEdgeFadeMask?.Dispose();
        rightEdgeFadeMask?.Dispose();
        leftEdgeFadeSourceBrush?.Dispose();
        rightEdgeFadeSourceBrush?.Dispose();
        leftEdgeFadeVisual?.Dispose();
        rightEdgeFadeVisual?.Dispose();
        leftEdgeSurface?.Dispose();
        rightEdgeSurface?.Dispose();
        edgeFadeContainer?.Dispose();
        leftEdgeFadeGradient = null;
        rightEdgeFadeGradient = null;
        leftEdgeFadeMask = null;
        rightEdgeFadeMask = null;
        leftEdgeFadeSourceBrush = null;
        rightEdgeFadeSourceBrush = null;
        leftEdgeFadeVisual = null;
        rightEdgeFadeVisual = null;
        leftEdgeSurface = null;
        rightEdgeSurface = null;
        edgeFadeContainer = null;
    }

    private ScrollViewer? GetScrollViewer()
    {
        ScrollViewer? currentScrollViewer = FindVisualDescendant<ScrollViewer>(GetHost().ModuleReorderList);

        if (currentScrollViewer is null)
        {
            return null;
        }

        AttachScrollViewer(currentScrollViewer);
        return currentScrollViewer;
    }

    private void AttachScrollViewer(ScrollViewer currentScrollViewer)
    {
        if (ReferenceEquals(scrollViewer, currentScrollViewer))
        {
            UpdateScrollButtons(currentScrollViewer);
            UpdateItemFade(currentScrollViewer);
            return;
        }

        if (scrollViewer is not null)
        {
            scrollViewer.ViewChanged -= HandleViewChanged;
        }

        scrollViewer = currentScrollViewer;
        currentScrollViewer.ViewChanged += HandleViewChanged;
        UpdateScrollButtons(currentScrollViewer);
        UpdateItemFade(currentScrollViewer);
    }

    private void HandleViewChanged(object? sender, ScrollViewerViewChangedEventArgs args)
    {
        if (sender is not ScrollViewer currentScrollViewer)
        {
            return;
        }

        centeredIndex = GetNearestIndex(currentScrollViewer);
        UpdateScrollButtons(currentScrollViewer);
        UpdateItemFade(currentScrollViewer);

        if (args.IsIntermediate)
        {
            return;
        }

        targetIndex = -1;
        double targetOffset = GetItemOffset(centeredIndex, currentScrollViewer);

        if (Math.Abs(currentScrollViewer.HorizontalOffset - targetOffset) > 0.5)
        {
            CenterItem(centeredIndex, false);
        }
    }

    private void Scroll(int direction)
    {
        ScrollViewer? currentScrollViewer = GetScrollViewer();

        if (currentScrollViewer is null)
        {
            return;
        }

        int currentIndex = targetIndex >= 0 ? targetIndex : centeredIndex >= 0 ? centeredIndex : GetNearestIndex(currentScrollViewer);
        CenterItem(currentIndex + direction, false);
    }

    private void UpdateScrollButtons(ScrollViewer? currentScrollViewer)
    {
        IDesktopIslandModuleReorderHost currentHost = GetHost();
        int itemCount = currentHost.ModuleOrder.Count;
        int currentCenteredIndex = targetIndex >= 0 ? targetIndex : currentScrollViewer is null ? centeredIndex : GetNearestIndex(currentScrollViewer);
        currentHost.PreviousModuleOrderButton.IsEnabled = itemCount > 1 && currentCenteredIndex > 0;
        currentHost.NextModuleOrderButton.IsEnabled = itemCount > 1 && currentCenteredIndex < itemCount - 1;
    }

    private void CenterItem(int index, bool disableAnimation)
    {
        IDesktopIslandModuleReorderHost currentHost = GetHost();
        ScrollViewer? currentScrollViewer = GetScrollViewer();
        int itemCount = currentHost.ModuleOrder.Count;

        if (currentScrollViewer is null || itemCount == 0)
        {
            return;
        }

        centeredIndex = Math.Clamp(index, 0, itemCount - 1);
        targetIndex = disableAnimation ? -1 : centeredIndex;
        double targetOffset = GetItemOffset(centeredIndex, currentScrollViewer);
        _ = currentScrollViewer.ChangeView(targetOffset, null, null, disableAnimation);
        UpdateScrollButtons(currentScrollViewer);
        UpdateItemFade(currentScrollViewer);
    }

    private int GetNearestIndex(ScrollViewer currentScrollViewer)
    {
        int itemCount = GetHost().ModuleOrder.Count;

        if (itemCount == 0)
        {
            return -1;
        }

        int index = (int)Math.Round(currentScrollViewer.HorizontalOffset / GetItemStride(), MidpointRounding.AwayFromZero);
        return Math.Clamp(index, 0, itemCount - 1);
    }

    private double GetItemOffset(int index, ScrollViewer currentScrollViewer) => Math.Clamp(index * GetItemStride(), 0, currentScrollViewer.ScrollableWidth);

    private double GetItemStride() => GetHost().ModuleReorderItemWidth + 2;

    private void UpdateItemFade(ScrollViewer? currentScrollViewer)
    {
        if (currentScrollViewer is null || currentScrollViewer.ViewportWidth <= 0)
        {
            return;
        }

        IDesktopIslandModuleReorderHost currentHost = GetHost();
        double viewportCenter = currentScrollViewer.ViewportWidth / 2;
        double fadeStart = GetItemStride() * 0.42;
        double fadeRange = Math.Max(1, viewportCenter - fadeStart);

        foreach (IGlanceComponent component in currentHost.ModuleOrder)
        {
            if (currentHost.ModuleReorderList.ContainerFromItem(component) is not ListViewItem item)
            {
                continue;
            }

            if (ReferenceEquals(item, draggedItem))
            {
                item.Opacity = 1;
                continue;
            }

            GeneralTransform transform = item.TransformToVisual(currentScrollViewer);
            Windows.Foundation.Point origin = transform.TransformPoint(new Windows.Foundation.Point());
            double itemCenter = origin.X + (item.ActualWidth / 2);
            double distance = Math.Abs(itemCenter - viewportCenter);
            double progress = Math.Clamp((distance - fadeStart) / fadeRange, 0, 1);
            item.Opacity = 1 - (progress * (1 - SideItemMinimumOpacity));
        }
    }

    private static T? FindVisualAncestor<T>(DependencyObject? element)
        where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T match)
            {
                return match;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    private static T? FindVisualDescendant<T>(DependencyObject element)
        where T : DependencyObject
    {
        int childCount = VisualTreeHelper.GetChildrenCount(element);

        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(element, index);

            if (child is T match)
            {
                return match;
            }

            T? descendant = FindVisualDescendant<T>(child);

            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}

