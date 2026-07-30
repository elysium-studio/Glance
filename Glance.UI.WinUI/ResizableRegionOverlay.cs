using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI;

namespace Glance.UI.WinUI;

public sealed partial class ResizableRegionOverlay :
    Canvas
{
    private const double CornerHandleSize = 56;
    private const double HandleLength = 28;
    private const double HandleSize = 32;
    private const double HandleThickness = 3;
    private const double MinimumCropSize = 48;
    public const double VisualPadding = HandleSize / 2;
    private readonly bool allowMove;
    private readonly Border bottomShade;
    private readonly Dictionary<CropInteraction, Thumb> handles;
    private readonly Dictionary<CropInteraction, FrameworkElement> handleVisuals;
    private readonly Border horizontalLineOne;
    private readonly Border horizontalLineTwo;
    private readonly Border leftShade;
    private readonly Border rightShade;
    private readonly Border selectionBorder;
    private readonly Border sizeHint;
    private readonly TextBlock sizeHintText;
    private readonly int sourceHeight;
    private readonly int sourceWidth;
    private readonly double surfaceHeight;
    private readonly double surfaceWidth;
    private readonly bool showShade;
    private readonly Border topShade;
    private readonly Border verticalLineOne;
    private readonly Border verticalLineTwo;
    private Rect cropBounds;
    private CropInteraction interaction;
    private Rect interactionBounds;
    private Point interactionPoint;
    private double resizeDeltaX;
    private double resizeDeltaY;

    public ResizableRegionOverlay(double width,
        double height,
        int sourceWidth,
        int sourceHeight,
        Rect? initialBounds = null,
        bool showShade = true,
        bool allowMove = true)
    {
        SolidColorBrush foreground = new(Color.FromArgb(255, 255, 255, 255));
        this.allowMove = allowMove;
        this.sourceWidth = sourceWidth;
        this.sourceHeight = sourceHeight;
        this.showShade = showShade;
        surfaceWidth = width;
        surfaceHeight = height;
        Width = width + (VisualPadding * 2);
        Height = height + (VisualPadding * 2);
        Background = allowMove ? new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) : null;

        topShade = CreateShade();
        bottomShade = CreateShade();
        leftShade = CreateShade();
        rightShade = CreateShade();
        selectionBorder = new Border
        {
            BorderBrush = foreground,
            BorderThickness = new Thickness(1),
            IsHitTestVisible = false
        };
        verticalLineOne = CreateGuide(foreground);
        verticalLineTwo = CreateGuide(foreground);
        horizontalLineOne = CreateGuide(foreground);
        horizontalLineTwo = CreateGuide(foreground);
        sizeHintText = new TextBlock
        {
            FontSize = 12,
            Foreground = foreground
        };
        sizeHint = new Border
        {
            Padding = new Thickness(8, 4, 8, 4),
            Background = new SolidColorBrush(Color.FromArgb(220, 0, 0, 0)),
            CornerRadius = new CornerRadius(2),
            Child = sizeHintText,
            IsHitTestVisible = false
        };
        Canvas.SetZIndex(sizeHint, 5);
        handles = new Dictionary<CropInteraction, Thumb>
        {
            [CropInteraction.TopLeft] = CreateHandle(CropInteraction.TopLeft),
            [CropInteraction.Top] = CreateHandle(CropInteraction.Top),
            [CropInteraction.TopRight] = CreateHandle(CropInteraction.TopRight),
            [CropInteraction.Right] = CreateHandle(CropInteraction.Right),
            [CropInteraction.BottomRight] = CreateHandle(CropInteraction.BottomRight),
            [CropInteraction.Bottom] = CreateHandle(CropInteraction.Bottom),
            [CropInteraction.BottomLeft] = CreateHandle(CropInteraction.BottomLeft),
            [CropInteraction.Left] = CreateHandle(CropInteraction.Left)
        };
        handleVisuals = new Dictionary<CropInteraction, FrameworkElement>
        {
            [CropInteraction.TopLeft] = CreateCornerHandle(CropInteraction.TopLeft, foreground),
            [CropInteraction.Top] = CreateEdgeHandle(CropInteraction.Top, foreground),
            [CropInteraction.TopRight] = CreateCornerHandle(CropInteraction.TopRight, foreground),
            [CropInteraction.Right] = CreateEdgeHandle(CropInteraction.Right, foreground),
            [CropInteraction.BottomRight] = CreateCornerHandle(CropInteraction.BottomRight, foreground),
            [CropInteraction.Bottom] = CreateEdgeHandle(CropInteraction.Bottom, foreground),
            [CropInteraction.BottomLeft] = CreateCornerHandle(CropInteraction.BottomLeft, foreground),
            [CropInteraction.Left] = CreateEdgeHandle(CropInteraction.Left, foreground)
        };

        Children.Add(topShade);
        Children.Add(bottomShade);
        Children.Add(leftShade);
        Children.Add(rightShade);
        Children.Add(selectionBorder);
        Children.Add(verticalLineOne);
        Children.Add(verticalLineTwo);
        Children.Add(horizontalLineOne);
        Children.Add(horizontalLineTwo);
        Children.Add(sizeHint);

        foreach (Thumb handle in handles.Values)
        {
            handle.DragStarted += HandleResizeDragStarted;
            handle.DragDelta += HandleResizeDragDelta;
            handle.DragCompleted += HandleResizeDragCompleted;
            Children.Add(handle);
        }

        foreach (FrameworkElement handleVisual in handleVisuals.Values)
        {
            Children.Add(handleVisual);
        }

        cropBounds = Clamp(initialBounds ?? new Rect(0, 0, width, height));
        UpdateVisuals();
        PointerPressed += HandlePointerPressed;
        PointerMoved += HandlePointerMoved;
        PointerReleased += HandlePointerReleased;
        PointerCanceled += HandlePointerCanceled;
        PointerCaptureLost += HandlePointerCaptureLost;
    }

    public Rect CropBounds => cropBounds;

    public event EventHandler? BoundsChanged;

    public event EventHandler? InteractionCompleted;

    public event EventHandler? InteractionStarted;

    private void HandlePointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (!allowMove || IsResizeHandle(args.OriginalSource))
        {
            return;
        }

        Point point = ToSurfacePoint(args.GetCurrentPoint(this).Position);
        if (!cropBounds.Contains(point) || !CapturePointer(args.Pointer))
        {
            return;
        }

        interaction = CropInteraction.Move;
        interactionPoint = point;
        interactionBounds = cropBounds;
        InteractionStarted?.Invoke(this, EventArgs.Empty);
        args.Handled = true;
    }

    private void HandlePointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (interaction != CropInteraction.Move)
        {
            return;
        }

        Point point = ToSurfacePoint(args.GetCurrentPoint(this).Position);
        double deltaX = point.X - interactionPoint.X;
        double deltaY = point.Y - interactionPoint.Y;
        cropBounds = CalculateBounds(interactionBounds, interaction, deltaX, deltaY);
        UpdateVisuals();
        BoundsChanged?.Invoke(this, EventArgs.Empty);
        args.Handled = true;
    }

    private void HandlePointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (interaction != CropInteraction.Move)
        {
            return;
        }

        interaction = CropInteraction.None;
        ReleasePointerCapture(args.Pointer);
        InteractionCompleted?.Invoke(this, EventArgs.Empty);
        args.Handled = true;
    }

    private void HandlePointerCanceled(object sender, PointerRoutedEventArgs args)
    {
        if (interaction != CropInteraction.Move)
        {
            return;
        }

        interaction = CropInteraction.None;
        ReleasePointerCapture(args.Pointer);
    }

    private void HandlePointerCaptureLost(object sender, PointerRoutedEventArgs args)
    {
        if (interaction == CropInteraction.Move)
        {
            interaction = CropInteraction.None;
        }
    }

    private void HandleResizeDragStarted(object sender, DragStartedEventArgs args)
    {
        if (sender is not FrameworkElement { Tag: CropInteraction mode })
        {
            return;
        }

        interaction = mode;
        interactionBounds = cropBounds;
        resizeDeltaX = 0;
        resizeDeltaY = 0;
        SetResizeFeedbackVisible(true);
        InteractionStarted?.Invoke(this, EventArgs.Empty);
    }

    private void HandleResizeDragDelta(object sender, DragDeltaEventArgs args)
    {
        if (interaction is CropInteraction.None or CropInteraction.Move)
        {
            return;
        }

        resizeDeltaX += args.HorizontalChange;
        resizeDeltaY += args.VerticalChange;
        cropBounds = CalculateBounds(interactionBounds, interaction, resizeDeltaX, resizeDeltaY);
        UpdateVisuals();
        BoundsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HandleResizeDragCompleted(object sender, DragCompletedEventArgs args)
    {
        if (interaction != CropInteraction.Move)
        {
            interaction = CropInteraction.None;
            SetResizeFeedbackVisible(false);
            InteractionCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool IsResizeHandle(object source)
    {
        DependencyObject? current = source as DependencyObject;

        while (current is not null && !ReferenceEquals(current, this))
        {
            if (current is Thumb)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static Point ToSurfacePoint(Point point) =>
        new(point.X - VisualPadding, point.Y - VisualPadding);

    private Rect CalculateBounds(Rect bounds, CropInteraction mode, double deltaX, double deltaY)
    {
        if (mode == CropInteraction.Move)
        {
            double x = Math.Clamp(bounds.X + deltaX, 0, surfaceWidth - bounds.Width);
            double y = Math.Clamp(bounds.Y + deltaY, 0, surfaceHeight - bounds.Height);
            return new Rect(x, y, bounds.Width, bounds.Height);
        }

        double left = bounds.X;
        double top = bounds.Y;
        double right = bounds.Right;
        double bottom = bounds.Bottom;

        if (mode is CropInteraction.Left or CropInteraction.TopLeft or CropInteraction.BottomLeft)
        {
            left = Math.Clamp(bounds.X + deltaX, 0, right - MinimumCropSize);
        }

        if (mode is CropInteraction.Right or CropInteraction.TopRight or CropInteraction.BottomRight)
        {
            right = Math.Clamp(bounds.Right + deltaX, left + MinimumCropSize, surfaceWidth);
        }

        if (mode is CropInteraction.Top or CropInteraction.TopLeft or CropInteraction.TopRight)
        {
            top = Math.Clamp(bounds.Y + deltaY, 0, bottom - MinimumCropSize);
        }

        if (mode is CropInteraction.Bottom or CropInteraction.BottomLeft or CropInteraction.BottomRight)
        {
            bottom = Math.Clamp(bounds.Bottom + deltaY, top + MinimumCropSize, surfaceHeight);
        }

        return new Rect(left, top, right - left, bottom - top);
    }

    private Rect Clamp(Rect value)
    {
        double width = Math.Clamp(value.Width, MinimumCropSize, surfaceWidth);
        double height = Math.Clamp(value.Height, MinimumCropSize, surfaceHeight);
        return new Rect(Math.Clamp(value.X, 0, surfaceWidth - width),
            Math.Clamp(value.Y, 0, surfaceHeight - height),
            width,
            height);
    }

    private void UpdateVisuals()
    {
        SetBounds(topShade, VisualPadding, VisualPadding, surfaceWidth, cropBounds.Y);
        SetBounds(bottomShade, VisualPadding, VisualPadding + cropBounds.Bottom, surfaceWidth, surfaceHeight - cropBounds.Bottom);
        SetBounds(leftShade, VisualPadding, VisualPadding + cropBounds.Y, cropBounds.X, cropBounds.Height);
        SetBounds(rightShade, VisualPadding + cropBounds.Right, VisualPadding + cropBounds.Y, surfaceWidth - cropBounds.Right, cropBounds.Height);
        Visibility shadeVisibility = showShade ? Visibility.Visible : Visibility.Collapsed;
        topShade.Visibility = shadeVisibility;
        bottomShade.Visibility = shadeVisibility;
        leftShade.Visibility = shadeVisibility;
        rightShade.Visibility = shadeVisibility;
        SetBounds(selectionBorder, VisualPadding + cropBounds.X, VisualPadding + cropBounds.Y, cropBounds.Width, cropBounds.Height);

        double verticalOne = cropBounds.X + (cropBounds.Width / 3);
        double verticalTwo = cropBounds.X + ((cropBounds.Width / 3) * 2);
        double horizontalOne = cropBounds.Y + (cropBounds.Height / 3);
        double horizontalTwo = cropBounds.Y + ((cropBounds.Height / 3) * 2);
        SetBounds(verticalLineOne, VisualPadding + verticalOne, VisualPadding + cropBounds.Y, 1, cropBounds.Height);
        SetBounds(verticalLineTwo, VisualPadding + verticalTwo, VisualPadding + cropBounds.Y, 1, cropBounds.Height);
        SetBounds(horizontalLineOne, VisualPadding + cropBounds.X, VisualPadding + horizontalOne, cropBounds.Width, 1);
        SetBounds(horizontalLineTwo, VisualPadding + cropBounds.X, VisualPadding + horizontalTwo, cropBounds.Width, 1);
        UpdateSizeHint();

        SetHandle(CropInteraction.TopLeft, cropBounds.X, cropBounds.Y);
        SetHandle(CropInteraction.Top, cropBounds.X + (cropBounds.Width / 2), cropBounds.Y);
        SetHandle(CropInteraction.TopRight, cropBounds.Right, cropBounds.Y);
        SetHandle(CropInteraction.Right, cropBounds.Right, cropBounds.Y + (cropBounds.Height / 2));
        SetHandle(CropInteraction.BottomRight, cropBounds.Right, cropBounds.Bottom);
        SetHandle(CropInteraction.Bottom, cropBounds.X + (cropBounds.Width / 2), cropBounds.Bottom);
        SetHandle(CropInteraction.BottomLeft, cropBounds.X, cropBounds.Bottom);
        SetHandle(CropInteraction.Left, cropBounds.X, cropBounds.Y + (cropBounds.Height / 2));
    }

    private void SetHandle(CropInteraction mode, double centerX, double centerY)
    {
        Thumb handle = handles[mode];
        double visualCenterX = VisualPadding + centerX;
        double visualCenterY = VisualPadding + centerY;
        double x = visualCenterX - (HandleSize / 2);
        double y = visualCenterY - (HandleSize / 2);
        SetBounds(handle, x, y, HandleSize, HandleSize);

        FrameworkElement handleVisual = handleVisuals[mode];
        Canvas.SetLeft(handleVisual, visualCenterX - (handleVisual.Width / 2));
        Canvas.SetTop(handleVisual, visualCenterY - (handleVisual.Height / 2));
    }

    private void SetResizeFeedbackVisible(bool visible)
    {
        Visibility visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        verticalLineOne.Visibility = visibility;
        verticalLineTwo.Visibility = visibility;
        horizontalLineOne.Visibility = visibility;
        horizontalLineTwo.Visibility = visibility;
    }

    private void UpdateSizeHint()
    {
        double scaleX = sourceWidth / surfaceWidth;
        double scaleY = sourceHeight / surfaceHeight;
        int left = Math.Clamp((int)Math.Floor(cropBounds.X * scaleX), 0, sourceWidth - 1);
        int top = Math.Clamp((int)Math.Floor(cropBounds.Y * scaleY), 0, sourceHeight - 1);
        int right = Math.Clamp((int)Math.Ceiling(cropBounds.Right * scaleX), left + 1, sourceWidth);
        int bottom = Math.Clamp((int)Math.Ceiling(cropBounds.Bottom * scaleY), top + 1, sourceHeight);
        sizeHintText.Text = $"{right - left} × {bottom - top}";
        sizeHintText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size textSize = sizeHintText.DesiredSize;
        double hintWidth = textSize.Width + sizeHint.Padding.Left + sizeHint.Padding.Right;
        double hintHeight = textSize.Height + sizeHint.Padding.Top + sizeHint.Padding.Bottom;
        double centerX = VisualPadding + cropBounds.X + (cropBounds.Width / 2);
        double bottomY = VisualPadding + cropBounds.Bottom;

        sizeHint.Width = hintWidth;
        sizeHint.Height = hintHeight;
        Canvas.SetLeft(sizeHint, centerX - (hintWidth / 2));
        Canvas.SetTop(sizeHint, bottomY - hintHeight - 10);
    }

    private static void SetBounds(FrameworkElement element, double x, double y, double width, double height)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        element.Width = Math.Max(0, width);
        element.Height = Math.Max(0, height);
    }

    private static Border CreateShade() =>
        new()
        {
            Background = new SolidColorBrush(Color.FromArgb(132, 0, 0, 0)),
            IsHitTestVisible = false
        };

    private static Border CreateGuide(Brush foreground) =>
        new()
        {
            Background = foreground,
            Opacity = 0.55,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };

    private static Thumb CreateHandle(CropInteraction mode)
    {
        Thumb handle = new()
        {
            Width = HandleSize,
            Height = HandleSize,
            Opacity = 0,
            IsTabStop = false,
            Tag = mode
        };

        Canvas.SetZIndex(handle, 3);
        return handle;
    }

    private static FrameworkElement CreateEdgeHandle(CropInteraction mode, Brush foreground)
    {
        bool horizontal = mode is CropInteraction.Top or CropInteraction.Bottom;
        Border visual = new()
        {
            Width = horizontal ? HandleLength : HandleThickness,
            Height = horizontal ? HandleThickness : HandleLength,
            Background = foreground,
            IsHitTestVisible = false
        };

        Canvas.SetZIndex(visual, 4);
        return visual;
    }

    private static FrameworkElement CreateCornerHandle(CropInteraction mode, Brush foreground)
    {
        bool left = mode is CropInteraction.TopLeft or CropInteraction.BottomLeft;
        bool top = mode is CropInteraction.TopLeft or CropInteraction.TopRight;
        Canvas visual = new()
        {
            Width = CornerHandleSize,
            Height = CornerHandleSize,
            IsHitTestVisible = false
        };
        Border horizontal = new()
        {
            Width = HandleLength,
            Height = HandleThickness,
            Background = foreground,
            IsHitTestVisible = false
        };
        Border vertical = new()
        {
            Width = HandleThickness,
            Height = HandleLength,
            Background = foreground,
            IsHitTestVisible = false
        };
        double center = CornerHandleSize / 2;

        Canvas.SetLeft(horizontal, left ? center : center - HandleLength);
        Canvas.SetTop(horizontal, center - (HandleThickness / 2));
        Canvas.SetLeft(vertical, center - (HandleThickness / 2));
        Canvas.SetTop(vertical, top ? center : center - HandleLength);
        visual.Children.Add(horizontal);
        visual.Children.Add(vertical);
        Canvas.SetZIndex(visual, 4);
        return visual;
    }

    private enum CropInteraction
    {
        None,
        Move,
        TopLeft,
        Top,
        TopRight,
        Right,
        BottomRight,
        Bottom,
        BottomLeft,
        Left
    }
}
