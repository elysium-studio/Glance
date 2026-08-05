using Elysium.Platform.Windows;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;
using WinUIEx;
using PlatformWindowExtensions = Elysium.Platform.Windows.WindowExtensions;

namespace Glance.ScreenRecorder.WinUI;

internal sealed class RecordingSelectionWindow
{
    private readonly IReadOnlyList<RecordingSelectionCandidate> candidates;
    private readonly RecordingSource? automaticSource;
    private readonly TaskCompletionSource<RecordingSelectionResult?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Border highlight;
    private readonly Border instructionContainer;
    private readonly ScreenRecordingMode mode;
    private readonly Grid root;
    private readonly Canvas regionCanvas;
    private readonly Canvas selectionCanvas;
    private readonly Microsoft.UI.Xaml.Shapes.Path smokeOverlay;
    private readonly RectangleGeometry smokeBounds = new();
    private readonly RectangleGeometry smokeCutout = new();
    private readonly Border toolbar;
    private readonly Canvas toolbarCanvas;
    private readonly Window window;
    private readonly nint windowHandle;
    private readonly NativeRectangle desktopBounds;
    private bool closed;
    private bool isDragging;
    private bool isRegionInteractionActive;
    private RecordingSource? pendingSource;
    private ResizableRegionOverlay? regionOverlay;
    private Rect regionSurfaceBounds;
    private Point selectionStart;

    private RecordingSelectionWindow(ScreenRecordingMode mode,
        IReadOnlyList<RecordingSelectionCandidate> candidates,
        NativeRectangle desktopBounds,
        ITextLocalizer localizer,
        RecordingSource? automaticSource)
    {
        this.mode = mode;
        this.candidates = candidates;
        this.desktopBounds = desktopBounds;
        this.automaticSource = automaticSource;

        GeometryGroup smokeGeometry = new() { FillRule = FillRule.EvenOdd };
        smokeGeometry.Children.Add(smokeBounds);
        smokeGeometry.Children.Add(smokeCutout);
        smokeOverlay = new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = smokeGeometry,
            Fill = ResolveSmokeBrush(),
            IsHitTestVisible = false
        };

        highlight = new Border
        {
            Background = new SolidColorBrush(Colors.Transparent),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 91, 103)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        selectionCanvas = new Canvas { IsHitTestVisible = false };
        selectionCanvas.Children.Add(highlight);
        regionCanvas = new Canvas();

        TextBlock instruction = new()
        {
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Colors.White),
            Text = mode switch
            {
                ScreenRecordingMode.Region => localizer.GetText("SelectRegionInstruction"),
                ScreenRecordingMode.Window => localizer.GetText("SelectWindowInstruction"),
                _ => localizer.GetText("SelectDisplayInstruction")
            }
        };
        instructionContainer = new Border
        {
            Margin = new Thickness(0, 32, 0, 0),
            Padding = new Thickness(16, 10, 16, 10),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Background = ResolveAcrylicBrush(),
            CornerRadius = new CornerRadius(8),
            Child = instruction,
            Translation = new System.Numerics.Vector3(0, 0, 32),
            Shadow = new ThemeShadow()
        };

        Button cancelButton = CreateToolbarButton("\uE711", localizer.GetText("CancelRecording"), false);
        cancelButton.Click += (_, _) => Complete(null);
        Button confirmButton = CreateToolbarButton("\uE73E", localizer.GetText("ConfirmRecording"), true);
        confirmButton.Click += (_, _) => ConfirmSelection();
        StackPanel toolbarContent = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        toolbarContent.Children.Add(cancelButton);
        toolbarContent.Children.Add(confirmButton);
        toolbar = new Border
        {
            Padding = new Thickness(6),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = OverlayChrome.CreateAcrylicBrush(),
            BorderBrush = ResolveSurfaceStrokeBrush(),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            Child = toolbarContent,
            Visibility = Visibility.Collapsed
        };
        OverlayChrome.Elevate(toolbar, 48);
        toolbarCanvas = new Canvas();
        toolbarCanvas.Children.Add(toolbar);

        root = new Grid
        {
            Background = new SolidColorBrush(Colors.Transparent),
            IsTabStop = true
        };
        root.Children.Add(smokeOverlay);
        root.Children.Add(selectionCanvas);
        root.Children.Add(regionCanvas);
        root.Children.Add(instructionContainer);
        root.Children.Add(toolbarCanvas);
        root.KeyDown += HandleKeyDown;
        root.Loaded += HandleLoaded;
        root.PointerMoved += HandlePointerMoved;
        root.PointerPressed += HandlePointerPressed;
        root.PointerReleased += HandlePointerReleased;
        root.SizeChanged += HandleSizeChanged;

        window = new Window
        {
            Content = root,
            ExtendsContentIntoTitleBar = true,
            SystemBackdrop = new TransparentTintBackdrop()
        };
        window.SetTitleBar(null);
        window.Closed += HandleClosed;
        windowHandle = WindowNative.GetWindowHandle(window);
    }

    public static Task<RecordingSelectionResult?> SelectAsync(ScreenRecordingMode mode,
        IReadOnlyList<RecordingSelectionCandidate> candidates,
        NativeRectangle desktopBounds,
        ITextLocalizer localizer,
        DispatcherQueue dispatcherQueue,
        RecordingSource? automaticSource = null)
    {
        TaskCompletionSource<RecordingSelectionResult?> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                RecordingSelectionWindow selectionWindow = new(mode, candidates, desktopBounds, localizer, automaticSource);
                _ = result.TrySetResult(await selectionWindow.ShowAsync());
            }
            catch (Exception exception)
            {
                _ = result.TrySetException(exception);
            }
        });
        return result.Task;
    }

    private Task<RecordingSelectionResult?> ShowAsync()
    {
        AppWindow appWindow = window.AppWindow;

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }

        PlatformWindowExtensions.SetBorderless(windowHandle, true);
        PlatformWindowExtensions.SetCornerRadius(windowHandle, WindowCornerPreference.Sharp);
        PlatformWindowExtensions.SetTopMost(windowHandle, true);
        appWindow.IsShownInSwitchers = false;
        appWindow.MoveAndResize(new RectInt32(desktopBounds.Left, desktopBounds.Top, desktopBounds.Width, desktopBounds.Height));
        window.Activate();
        _ = root.Focus(FocusState.Programmatic);
        return completion.Task;
    }

    private void HandleKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Escape)
        {
            args.Handled = true;
            Complete(null);
        }
    }

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        root.Loaded -= HandleLoaded;

        if (automaticSource is not null)
        {
            ReviewSelection(automaticSource);
        }
    }

    private void HandlePointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (pendingSource is not null)
        {
            return;
        }

        Point point = args.GetCurrentPoint(root).Position;

        if (mode == ScreenRecordingMode.Region)
        {
            if (isDragging)
            {
                ShowHighlight(CreateRectangle(selectionStart, point));
            }

            return;
        }

        RecordingSelectionCandidate? candidate = FindCandidate(point);

        if (candidate is null)
        {
            ClearHighlight();
        }
        else
        {
            ShowHighlight(ToLocal(candidate.Bounds));
        }
    }

    private void HandlePointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (pendingSource is not null)
        {
            return;
        }

        Point point = args.GetCurrentPoint(root).Position;

        if (mode == ScreenRecordingMode.Region)
        {
            selectionStart = point;
            isDragging = true;
            _ = root.CapturePointer(args.Pointer);
            ShowHighlight(CreateRectangle(point, point));
            return;
        }

        RecordingSelectionCandidate? candidate = FindCandidate(point);

        if (candidate is not null)
        {
            ReviewSelection(new RecordingSource(mode, candidate.Bounds, candidate.WindowHandle, candidate.MonitorHandle));
        }
    }

    private void HandlePointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (mode != ScreenRecordingMode.Region || !isDragging)
        {
            return;
        }

        isDragging = false;
        root.ReleasePointerCapture(args.Pointer);
        Rect selection = CreateRectangle(selectionStart, args.GetCurrentPoint(root).Position);

        if (selection.Width < 8 || selection.Height < 8)
        {
            ClearHighlight();
            return;
        }

        NativeRectangle bounds = ToScreen(selection);
        RecordingSelectionCandidate? display = candidates.FirstOrDefault(candidate =>
            candidate.Bounds.Left <= bounds.Left && candidate.Bounds.Top <= bounds.Top &&
            candidate.Bounds.Right >= bounds.Right && candidate.Bounds.Bottom >= bounds.Bottom);
        display ??= candidates.OrderByDescending(candidate => IntersectionArea(candidate.Bounds, bounds)).FirstOrDefault();

        if (display is not null)
        {
            NativeRectangle captureBounds = Intersect(display.Bounds, bounds);
            ReviewSelection(new RecordingSource(mode, captureBounds, nint.Zero, display.MonitorHandle));
        }
    }

    private void HandleSizeChanged(object sender, SizeChangedEventArgs args) => smokeBounds.Rect = new Rect(0, 0, root.ActualWidth, root.ActualHeight);

    private void HandleClosed(object sender, WindowEventArgs args)
    {
        closed = true;
        _ = completion.TrySetResult(null);
    }

    private RecordingSelectionCandidate? FindCandidate(Point point)
    {
        (int x, int y) = ToScreen(point);
        return candidates.FirstOrDefault(candidate =>
            x >= candidate.Bounds.Left && x < candidate.Bounds.Right &&
            y >= candidate.Bounds.Top && y < candidate.Bounds.Bottom);
    }

    public Task<RecordingReviewWindow?> ReviewAsync(string filePath,
        NativeRectangle sourceBounds,
        ITextLocalizer reviewLocalizer) => RecordingReviewWindow.ReviewAsync(filePath,
            sourceBounds,
            desktopBounds,
            reviewLocalizer,
            window,
            root,
            windowHandle);

    public void Close()
    {
        if (window.DispatcherQueue.HasThreadAccess)
        {
            CloseCore();
        }
        else
        {
            _ = window.DispatcherQueue.TryEnqueue(CloseCore);
        }
    }

    private void Complete(RecordingSource? source)
    {
        if (closed)
        {
            return;
        }

        if (source is null)
        {
            _ = completion.TrySetResult(null);
            CloseCore();
            return;
        }

        PrepareForReuse();
        PlatformWindowExtensions.viSetOpacity(windowHandle, 0);
        window.AppWindow.MoveAndResize(new RectInt32(-32000,
            -32000,
            desktopBounds.Width,
            desktopBounds.Height));
        _ = completion.TrySetResult(new RecordingSelectionResult(source, this));
    }

    private void PrepareForReuse()
    {
        root.ReleasePointerCaptures();
        root.KeyDown -= HandleKeyDown;
        root.Loaded -= HandleLoaded;
        root.PointerMoved -= HandlePointerMoved;
        root.PointerPressed -= HandlePointerPressed;
        root.PointerReleased -= HandlePointerReleased;
        root.SizeChanged -= HandleSizeChanged;
        window.Closed -= HandleClosed;

        if (regionOverlay is not null)
        {
            regionOverlay.BoundsChanged -= HandleRegionBoundsChanged;
            regionOverlay.InteractionCompleted -= HandleRegionInteractionCompleted;
            regionOverlay.InteractionStarted -= HandleRegionInteractionStarted;
            _ = regionCanvas.Children.Remove(regionOverlay);
        }
    }

    private void CloseCore()
    {
        if (closed)
        {
            return;
        }

        closed = true;
        PrepareForReuse();

        try
        {
            window.AppWindow.Hide();
            window.Close();
        }
        catch (COMException)
        {
        }
    }

    private void ConfirmSelection()
    {
        if (pendingSource is not RecordingSource source)
        {
            return;
        }

        if (regionOverlay is not null)
        {
            Rect selection = regionOverlay.CropBounds;
            source = source with
            {
                Bounds = ToScreen(new Rect(regionSurfaceBounds.X + selection.X,
                    regionSurfaceBounds.Y + selection.Y,
                    selection.Width,
                    selection.Height))
            };
        }

        Complete(source);
    }

    private void ReviewSelection(RecordingSource source)
    {
        pendingSource = source;
        instructionContainer.Visibility = Visibility.Collapsed;
        Rect selection = ToLocal(source.Bounds);

        if (source.Mode == ScreenRecordingMode.Region)
        {
            RecordingSelectionCandidate? display = candidates.FirstOrDefault(candidate => candidate.MonitorHandle == source.MonitorHandle);
            Rect displayBounds = display is null ? new Rect(0, 0, root.ActualWidth, root.ActualHeight) : ToLocal(display.Bounds);
            regionSurfaceBounds = displayBounds;
            Rect initialBounds = new(selection.X - displayBounds.X,
                selection.Y - displayBounds.Y,
                selection.Width,
                selection.Height);
            regionOverlay = new ResizableRegionOverlay(displayBounds.Width,
                displayBounds.Height,
                display?.Bounds.Width ?? desktopBounds.Width,
                display?.Bounds.Height ?? desktopBounds.Height,
                initialBounds,
                showShade: false);
            regionOverlay.BoundsChanged += HandleRegionBoundsChanged;
            regionOverlay.InteractionCompleted += HandleRegionInteractionCompleted;
            regionOverlay.InteractionStarted += HandleRegionInteractionStarted;
            Canvas.SetLeft(regionOverlay, displayBounds.X - ResizableRegionOverlay.VisualPadding);
            Canvas.SetTop(regionOverlay, displayBounds.Y - ResizableRegionOverlay.VisualPadding);
            selectionCanvas.Visibility = Visibility.Collapsed;
            regionCanvas.Children.Add(regionOverlay);
        }

        smokeCutout.Rect = selection;
        toolbar.Visibility = Visibility.Visible;
        PositionToolbar(selection);
        _ = toolbar.Focus(FocusState.Programmatic);
    }

    private void HandleRegionBoundsChanged(object? sender, EventArgs args)
    {
        if (regionOverlay is null)
        {
            return;
        }

        Rect selection = regionOverlay.CropBounds;
        Rect positionedSelection = new(regionSurfaceBounds.X + selection.X,
            regionSurfaceBounds.Y + selection.Y,
            selection.Width,
            selection.Height);
        smokeCutout.Rect = positionedSelection;

        if (!isRegionInteractionActive)
        {
            PositionToolbar(positionedSelection);
        }
    }

    private void HandleRegionInteractionStarted(object? sender, EventArgs args)
    {
        isRegionInteractionActive = true;
        toolbar.Visibility = Visibility.Collapsed;
    }

    private void HandleRegionInteractionCompleted(object? sender, EventArgs args)
    {
        isRegionInteractionActive = false;
        toolbar.Visibility = Visibility.Collapsed;
        _ = root.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, CompleteRegionInteraction);
    }

    private void CompleteRegionInteraction()
    {
        if (isRegionInteractionActive || regionOverlay is null)
        {
            return;
        }

        toolbar.Visibility = Visibility.Visible;
        HandleRegionBoundsChanged(regionOverlay, EventArgs.Empty);
    }

    private void PositionToolbar(Rect selection)
    {
        toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double width = toolbar.DesiredSize.Width;
        double height = toolbar.DesiredSize.Height;
        double left = Math.Clamp(selection.X + ((selection.Width - width) / 2), 20, Math.Max(20, root.ActualWidth - width - 20));
        double top = selection.Y - height - 14;

        if (top < 20)
        {
            top = selection.Bottom + 14;
        }

        if (top + height > root.ActualHeight - 20)
        {
            top = Math.Max(20, selection.Y + 14);
        }

        Canvas.SetLeft(toolbar, left);
        Canvas.SetTop(toolbar, top);
    }

    private void ShowHighlight(Rect rectangle)
    {
        smokeCutout.Rect = rectangle;
        Canvas.SetLeft(highlight, rectangle.X);
        Canvas.SetTop(highlight, rectangle.Y);
        highlight.Width = rectangle.Width;
        highlight.Height = rectangle.Height;
        highlight.Visibility = Visibility.Visible;
    }

    private void ClearHighlight()
    {
        smokeCutout.Rect = Rect.Empty;
        highlight.Visibility = Visibility.Collapsed;
    }

    private Rect ToLocal(NativeRectangle bounds)
    {
        double scaleX = root.ActualWidth / desktopBounds.Width;
        double scaleY = root.ActualHeight / desktopBounds.Height;
        return new Rect((bounds.Left - desktopBounds.Left) * scaleX,
            (bounds.Top - desktopBounds.Top) * scaleY,
            bounds.Width * scaleX,
            bounds.Height * scaleY);
    }

    private NativeRectangle ToScreen(Rect rectangle)
    {
        double scaleX = desktopBounds.Width / root.ActualWidth;
        double scaleY = desktopBounds.Height / root.ActualHeight;
        int left = desktopBounds.Left + (int)Math.Round(rectangle.X * scaleX);
        int top = desktopBounds.Top + (int)Math.Round(rectangle.Y * scaleY);
        return new NativeRectangle(left,
            top,
            left + (int)Math.Round(rectangle.Width * scaleX),
            top + (int)Math.Round(rectangle.Height * scaleY));
    }

    private (int X, int Y) ToScreen(Point point)
    {
        double scaleX = desktopBounds.Width / root.ActualWidth;
        double scaleY = desktopBounds.Height / root.ActualHeight;
        return (desktopBounds.Left + (int)Math.Round(point.X * scaleX),
            desktopBounds.Top + (int)Math.Round(point.Y * scaleY));
    }

    private static Rect CreateRectangle(Point start, Point end) => new(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));

    private static long IntersectionArea(NativeRectangle first, NativeRectangle second)
    {
        int width = Math.Max(0, Math.Min(first.Right, second.Right) - Math.Max(first.Left, second.Left));
        int height = Math.Max(0, Math.Min(first.Bottom, second.Bottom) - Math.Max(first.Top, second.Top));
        return (long)width * height;
    }

    private static NativeRectangle Intersect(NativeRectangle first, NativeRectangle second) => new(Math.Max(first.Left, second.Left),
            Math.Max(first.Top, second.Top),
            Math.Min(first.Right, second.Right),
            Math.Min(first.Bottom, second.Bottom));

    private static Brush ResolveSmokeBrush() => Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("SmokeFillColorDefaultBrush", out object value) && value is Brush brush
            ? brush
            : new SolidColorBrush(Color.FromArgb(92, 0, 0, 0));

    private static Brush ResolveAcrylicBrush() => Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("AcrylicInAppFillColorDefaultBrush", out object value) && value is Brush brush
            ? brush
            : new SolidColorBrush(Color.FromArgb(235, 30, 30, 30));

    private static Brush ResolveSurfaceStrokeBrush() => Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("SurfaceStrokeColorDefaultBrush", out object value) && value is Brush brush
            ? brush
            : new SolidColorBrush(Color.FromArgb(48, 255, 255, 255));

    private static Button CreateToolbarButton(string glyph, string label, bool accent)
    {
        Button button = new()
        {
            Width = 36,
            Height = 36,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(18),
            Content = new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 14,
                Glyph = glyph
            }
        };

        if (accent && Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("AccentButtonStyle", out object value) && value is Style style)
        {
            button.Style = style;
        }

        AutomationProperties.SetName(button, label);
        ToolTipService.SetToolTip(button, label);
        return button;
    }
}
