using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace Glance.ScreenLens.WinUI;

internal sealed class LensRegionAdjuster :
    Canvas
{
    private const double HandleHitSize = 32;
    private const double HandleVisualSize = 10;
    private const double MinimumSize = 48;
    private readonly Dictionary<ResizeMode, Thumb> handles;
    private readonly Dictionary<ResizeMode, Ellipse> handleVisuals;
    private readonly Border selectionBorder;
    private readonly double surfaceHeight;
    private readonly double surfaceWidth;
    private Rect bounds;
    private Rect dragBounds;
    private ResizeMode dragMode;
    private double dragX;
    private double dragY;

    public LensRegionAdjuster(double surfaceWidth, double surfaceHeight, Rect initialBounds)
    {
        this.surfaceWidth = surfaceWidth;
        this.surfaceHeight = surfaceHeight;
        Width = surfaceWidth;
        Height = surfaceHeight;
        bounds = Clamp(initialBounds);

        selectionBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            IsHitTestVisible = false
        };
        handles = new Dictionary<ResizeMode, Thumb>
        {
            [ResizeMode.TopLeft] = CreateThumb(ResizeMode.TopLeft),
            [ResizeMode.Top] = CreateThumb(ResizeMode.Top),
            [ResizeMode.TopRight] = CreateThumb(ResizeMode.TopRight),
            [ResizeMode.Right] = CreateThumb(ResizeMode.Right),
            [ResizeMode.BottomRight] = CreateThumb(ResizeMode.BottomRight),
            [ResizeMode.Bottom] = CreateThumb(ResizeMode.Bottom),
            [ResizeMode.BottomLeft] = CreateThumb(ResizeMode.BottomLeft),
            [ResizeMode.Left] = CreateThumb(ResizeMode.Left)
        };
        handleVisuals = handles.Keys.ToDictionary(mode => mode, _ => CreateHandleVisual());

        Children.Add(selectionBorder);

        foreach (Thumb handle in handles.Values)
        {
            Children.Add(handle);
        }

        foreach (Ellipse visual in handleVisuals.Values)
        {
            Children.Add(visual);
        }

        UpdateVisuals();
    }

    public Rect Bounds => bounds;

    public event EventHandler? BoundsChanged;

    public event EventHandler? InteractionCompleted;

    public event EventHandler? InteractionStarted;

    private static Ellipse CreateHandleVisual() =>
        new()
        {
            Width = HandleVisualSize,
            Height = HandleVisualSize,
            Fill = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
            Stroke = new SolidColorBrush(Color.FromArgb(96, 0, 0, 0)),
            StrokeThickness = 1,
            IsHitTestVisible = false
        };

    private Thumb CreateThumb(ResizeMode mode)
    {
        Thumb thumb = new()
        {
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
            IsTabStop = false,
            Tag = mode
        };
        thumb.DragStarted += HandleDragStarted;
        thumb.DragDelta += HandleDragDelta;
        thumb.DragCompleted += HandleDragCompleted;
        Canvas.SetZIndex(thumb, 3);
        return thumb;
    }

    private Rect CalculateBounds(Rect original, ResizeMode mode, double deltaX, double deltaY)
    {
        double left = original.X;
        double top = original.Y;
        double right = original.Right;
        double bottom = original.Bottom;

        if (mode is ResizeMode.Left or ResizeMode.TopLeft or ResizeMode.BottomLeft)
        {
            left = Math.Clamp(original.X + deltaX, 0, right - MinimumSize);
        }

        if (mode is ResizeMode.Right or ResizeMode.TopRight or ResizeMode.BottomRight)
        {
            right = Math.Clamp(original.Right + deltaX, left + MinimumSize, surfaceWidth);
        }

        if (mode is ResizeMode.Top or ResizeMode.TopLeft or ResizeMode.TopRight)
        {
            top = Math.Clamp(original.Y + deltaY, 0, bottom - MinimumSize);
        }

        if (mode is ResizeMode.Bottom or ResizeMode.BottomLeft or ResizeMode.BottomRight)
        {
            bottom = Math.Clamp(original.Bottom + deltaY, top + MinimumSize, surfaceHeight);
        }

        return new Rect(left, top, right - left, bottom - top);
    }

    private Rect Clamp(Rect value)
    {
        double width = Math.Clamp(value.Width, MinimumSize, surfaceWidth);
        double height = Math.Clamp(value.Height, MinimumSize, surfaceHeight);
        return new Rect(Math.Clamp(value.X, 0, surfaceWidth - width),
            Math.Clamp(value.Y, 0, surfaceHeight - height),
            width,
            height);
    }

    private void HandleDragCompleted(object sender, DragCompletedEventArgs args)
    {
        dragMode = ResizeMode.None;
        InteractionCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void HandleDragDelta(object sender, DragDeltaEventArgs args)
    {
        dragX += args.HorizontalChange;
        dragY += args.VerticalChange;
        bounds = CalculateBounds(dragBounds, dragMode, dragX, dragY);
        UpdateVisuals();
        BoundsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HandleDragStarted(object sender, DragStartedEventArgs args)
    {
        if (sender is not FrameworkElement { Tag: ResizeMode mode })
        {
            return;
        }

        dragMode = mode;
        dragBounds = bounds;
        dragX = 0;
        dragY = 0;
        InteractionStarted?.Invoke(this, EventArgs.Empty);
    }

    private void PositionHandle(ResizeMode mode, double x, double y)
    {
        Thumb handle = handles[mode];
        Ellipse visual = handleVisuals[mode];
        SetBounds(handle, x - (HandleHitSize / 2), y - (HandleHitSize / 2), HandleHitSize, HandleHitSize);
        SetBounds(visual, x - (HandleVisualSize / 2), y - (HandleVisualSize / 2), HandleVisualSize, HandleVisualSize);
    }

    private static void SetBounds(FrameworkElement element, double x, double y, double width, double height)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        element.Width = Math.Max(0, width);
        element.Height = Math.Max(0, height);
    }

    private void UpdateVisuals()
    {
        SetBounds(selectionBorder, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        PositionHandle(ResizeMode.TopLeft, bounds.X, bounds.Y);
        PositionHandle(ResizeMode.Top, bounds.X + (bounds.Width / 2), bounds.Y);
        PositionHandle(ResizeMode.TopRight, bounds.Right, bounds.Y);
        PositionHandle(ResizeMode.Right, bounds.Right, bounds.Y + (bounds.Height / 2));
        PositionHandle(ResizeMode.BottomRight, bounds.Right, bounds.Bottom);
        PositionHandle(ResizeMode.Bottom, bounds.X + (bounds.Width / 2), bounds.Bottom);
        PositionHandle(ResizeMode.BottomLeft, bounds.X, bounds.Bottom);
        PositionHandle(ResizeMode.Left, bounds.X, bounds.Y + (bounds.Height / 2));
    }

    private enum ResizeMode
    {
        None,
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
