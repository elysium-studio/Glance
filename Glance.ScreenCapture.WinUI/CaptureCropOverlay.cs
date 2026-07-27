using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI;

namespace Glance.ScreenCapture.WinUI;

internal sealed class CaptureCropOverlay :
    Canvas
{
    private const double HandleSize = 12;
    private const double HitTargetSize = 18;
    private const double MinimumCropSize = 48;
    private readonly Border bottomShade;
    private readonly Dictionary<CropInteraction, Border> handles;
    private readonly Border horizontalLineOne;
    private readonly Border horizontalLineTwo;
    private readonly Border leftShade;
    private readonly Border rightShade;
    private readonly Border selectionBorder;
    private readonly double surfaceHeight;
    private readonly double surfaceWidth;
    private readonly Border topShade;
    private readonly Border verticalLineOne;
    private readonly Border verticalLineTwo;
    private Rect cropBounds;
    private CropInteraction interaction;
    private Rect interactionBounds;
    private Point interactionPoint;

    public CaptureCropOverlay(double width, double height, Brush foreground)
    {
        surfaceWidth = width;
        surfaceHeight = height;
        Width = width;
        Height = height;
        Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
        Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, height) };

        topShade = CreateShade();
        bottomShade = CreateShade();
        leftShade = CreateShade();
        rightShade = CreateShade();
        selectionBorder = new Border
        {
            BorderBrush = foreground,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            IsHitTestVisible = false
        };
        verticalLineOne = CreateGuide(foreground);
        verticalLineTwo = CreateGuide(foreground);
        horizontalLineOne = CreateGuide(foreground);
        horizontalLineTwo = CreateGuide(foreground);
        handles = new Dictionary<CropInteraction, Border>
        {
            [CropInteraction.TopLeft] = CreateHandle(foreground),
            [CropInteraction.Top] = CreateHandle(foreground),
            [CropInteraction.TopRight] = CreateHandle(foreground),
            [CropInteraction.Right] = CreateHandle(foreground),
            [CropInteraction.BottomRight] = CreateHandle(foreground),
            [CropInteraction.Bottom] = CreateHandle(foreground),
            [CropInteraction.BottomLeft] = CreateHandle(foreground),
            [CropInteraction.Left] = CreateHandle(foreground)
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

        foreach (Border handle in handles.Values)
        {
            Children.Add(handle);
        }

        cropBounds = new Rect(0, 0, width, height);
        UpdateVisuals();
        PointerPressed += HandlePointerPressed;
        PointerMoved += HandlePointerMoved;
        PointerReleased += HandlePointerReleased;
        PointerCanceled += HandlePointerCanceled;
        PointerCaptureLost += HandlePointerCaptureLost;
    }

    public Rect CropBounds => cropBounds;

    private void HandlePointerPressed(object sender, PointerRoutedEventArgs args)
    {
        Point point = args.GetCurrentPoint(this).Position;
        interaction = ResolveInteraction(point);

        if (interaction == CropInteraction.None)
        {
            return;
        }

        interactionPoint = point;
        interactionBounds = cropBounds;
        CapturePointer(args.Pointer);
        args.Handled = true;
    }

    private void HandlePointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (interaction == CropInteraction.None)
        {
            return;
        }

        Point point = args.GetCurrentPoint(this).Position;
        double deltaX = point.X - interactionPoint.X;
        double deltaY = point.Y - interactionPoint.Y;
        cropBounds = CalculateBounds(interactionBounds, interaction, deltaX, deltaY);
        UpdateVisuals();
        args.Handled = true;
    }

    private void HandlePointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (interaction == CropInteraction.None)
        {
            return;
        }

        interaction = CropInteraction.None;
        ReleasePointerCapture(args.Pointer);
        args.Handled = true;
    }

    private void HandlePointerCanceled(object sender, PointerRoutedEventArgs args)
    {
        interaction = CropInteraction.None;
        ReleasePointerCapture(args.Pointer);
    }

    private void HandlePointerCaptureLost(object sender, PointerRoutedEventArgs args) =>
        interaction = CropInteraction.None;

    private CropInteraction ResolveInteraction(Point point)
    {
        bool nearLeft = Math.Abs(point.X - cropBounds.X) <= HitTargetSize;
        bool nearRight = Math.Abs(point.X - cropBounds.Right) <= HitTargetSize;
        bool nearTop = Math.Abs(point.Y - cropBounds.Y) <= HitTargetSize;
        bool nearBottom = Math.Abs(point.Y - cropBounds.Bottom) <= HitTargetSize;
        bool withinHorizontalRange = point.X >= cropBounds.X - HitTargetSize && point.X <= cropBounds.Right + HitTargetSize;
        bool withinVerticalRange = point.Y >= cropBounds.Y - HitTargetSize && point.Y <= cropBounds.Bottom + HitTargetSize;

        if (nearLeft && nearTop)
        {
            return CropInteraction.TopLeft;
        }

        if (nearRight && nearTop)
        {
            return CropInteraction.TopRight;
        }

        if (nearRight && nearBottom)
        {
            return CropInteraction.BottomRight;
        }

        if (nearLeft && nearBottom)
        {
            return CropInteraction.BottomLeft;
        }

        if (nearTop && withinHorizontalRange)
        {
            return CropInteraction.Top;
        }

        if (nearRight && withinVerticalRange)
        {
            return CropInteraction.Right;
        }

        if (nearBottom && withinHorizontalRange)
        {
            return CropInteraction.Bottom;
        }

        if (nearLeft && withinVerticalRange)
        {
            return CropInteraction.Left;
        }

        return cropBounds.Contains(point) ? CropInteraction.Move : CropInteraction.None;
    }

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

    private void UpdateVisuals()
    {
        SetBounds(topShade, 0, 0, surfaceWidth, cropBounds.Y);
        SetBounds(bottomShade, 0, cropBounds.Bottom, surfaceWidth, surfaceHeight - cropBounds.Bottom);
        SetBounds(leftShade, 0, cropBounds.Y, cropBounds.X, cropBounds.Height);
        SetBounds(rightShade, cropBounds.Right, cropBounds.Y, surfaceWidth - cropBounds.Right, cropBounds.Height);
        SetBounds(selectionBorder, cropBounds.X, cropBounds.Y, cropBounds.Width, cropBounds.Height);

        double verticalOne = cropBounds.X + (cropBounds.Width / 3);
        double verticalTwo = cropBounds.X + ((cropBounds.Width / 3) * 2);
        double horizontalOne = cropBounds.Y + (cropBounds.Height / 3);
        double horizontalTwo = cropBounds.Y + ((cropBounds.Height / 3) * 2);
        SetBounds(verticalLineOne, verticalOne, cropBounds.Y, 1, cropBounds.Height);
        SetBounds(verticalLineTwo, verticalTwo, cropBounds.Y, 1, cropBounds.Height);
        SetBounds(horizontalLineOne, cropBounds.X, horizontalOne, cropBounds.Width, 1);
        SetBounds(horizontalLineTwo, cropBounds.X, horizontalTwo, cropBounds.Width, 1);

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
        Border handle = handles[mode];
        double x = Math.Clamp(centerX - (HandleSize / 2), 0, surfaceWidth - HandleSize);
        double y = Math.Clamp(centerY - (HandleSize / 2), 0, surfaceHeight - HandleSize);
        SetBounds(handle, x, y, HandleSize, HandleSize);
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
            Opacity = 0.5,
            IsHitTestVisible = false
        };

    private static Border CreateHandle(Brush foreground) =>
        new()
        {
            Width = HandleSize,
            Height = HandleSize,
            Background = foreground,
            BorderBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(HandleSize / 2),
            IsHitTestVisible = false
        };

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
