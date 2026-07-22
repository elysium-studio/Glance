using Elysium.Platform.Windows;
using Glance.Application.Abstractions;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using WinRT.Interop;
using WinUIEx;
using PlatformWindowExtensions = Elysium.Platform.Windows.WindowExtensions;

namespace Glance.ScreenCapture.WinUI;

internal sealed class CaptureSelectionWindow
{
    private const int CaptureBeatDurationMs = 83;
    private const int CaptureHoldDurationMs = 50;
    private const int FlightDurationMs = 250;
    private const int AnimationDurationMs = CaptureBeatDurationMs + CaptureHoldDurationMs + FlightDurationMs;

    private readonly DesktopCaptureBitmap bitmap;
    private readonly IReadOnlyList<CaptureSelectionCandidate> candidates;
    private readonly TaskCompletionSource<CaptureSelectionResult?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Canvas flightCanvas;
    private readonly Border highlight;
    private readonly ScreenCaptureMode mode;
    private readonly Grid root;
    private readonly Grid selectionChrome;
    private readonly RectangleGeometry smokeBounds;
    private readonly RectangleGeometry smokeCutout;
    private readonly Window window;
    private readonly nint windowHandle;
    private bool closed;
    private bool flightInProgress;
    private bool isDragging;
    private bool isPositioned;
    private bool isShown;
    private int renderedFrameCount;
    private bool selectionCompleted;
    private Point selectionStart;

    private CaptureSelectionWindow(DesktopCaptureBitmap bitmap, ScreenCaptureMode mode, IReadOnlyList<CaptureSelectionCandidate> candidates, ITextLocalizer localizer, ImageSource imageSource)
    {
        this.bitmap = bitmap;
        this.mode = mode;
        this.candidates = candidates;

        smokeBounds = new RectangleGeometry();
        smokeCutout = new RectangleGeometry();
        GeometryGroup smokeGeometry = new() { FillRule = FillRule.EvenOdd };
        smokeGeometry.Children.Add(smokeBounds);
        smokeGeometry.Children.Add(smokeCutout);
        Microsoft.UI.Xaml.Shapes.Path smokeOverlay = new()
        {
            Data = smokeGeometry,
            Fill = ResolveSmokeBrush(),
            IsHitTestVisible = false
        };
        highlight = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 104, 216, 255)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        Canvas selectionCanvas = new() { IsHitTestVisible = false };
        selectionCanvas.Children.Add(highlight);

        TextBlock instruction = new()
        {
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255)),
            Text = mode switch
            {
                ScreenCaptureMode.Region => localizer.GetText("SelectRegionInstruction"),
                ScreenCaptureMode.Window => localizer.GetText("SelectWindowInstruction"),
                ScreenCaptureMode.Display => localizer.GetText("SelectDisplayInstruction"),
                _ => string.Empty
            }
        };
        Border instructionContainer = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 24, 0, 0),
            Padding = new Thickness(14, 8, 14, 8),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(220, 24, 24, 24)),
            CornerRadius = new CornerRadius(8),
            Child = instruction
        };
        selectionChrome = new Grid { IsHitTestVisible = false };
        selectionChrome.Children.Add(smokeOverlay);
        selectionChrome.Children.Add(selectionCanvas);
        selectionChrome.Children.Add(instructionContainer);
        flightCanvas = new Canvas { IsHitTestVisible = false };
        root = new Grid
        {
            Background = new ImageBrush { ImageSource = imageSource, Stretch = Stretch.Fill },
            IsTabStop = true
        };
        root.Children.Add(selectionChrome);
        root.Children.Add(flightCanvas);
        root.KeyDown += HandleKeyDown;
        root.PointerMoved += HandlePointerMoved;
        root.PointerPressed += HandlePointerPressed;
        root.PointerReleased += HandlePointerReleased;
        root.Loaded += HandleRootLoaded;
        root.SizeChanged += HandleRootSizeChanged;

        if (mode == ScreenCaptureMode.AllDisplays)
        {
            selectionChrome.Visibility = Visibility.Collapsed;
        }

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

    public static Task<CaptureSelectionResult?> SelectAsync(DesktopCaptureBitmap bitmap, ScreenCaptureMode mode, IReadOnlyList<CaptureSelectionCandidate> candidates, ITextLocalizer localizer, DispatcherQueue dispatcherQueue) =>
        ShowOnDispatcherAsync(bitmap, mode, candidates, localizer, dispatcherQueue);

    public Task PlayFlightAsync(DesktopCaptureBitmap capture, NativeRectangle landingBounds, Action onArrived)
    {
        TaskCompletionSource<bool> flightCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        void Play()
        {
            try
            {
                PlayFlight(capture, landingBounds, onArrived, flightCompletion);
            }
            catch (Exception exception)
            {
                CloseCore();
                flightCompletion.TrySetException(exception);
            }
        }

        if (window.DispatcherQueue.HasThreadAccess)
        {
            Play();
        }
        else if (!window.DispatcherQueue.TryEnqueue(Play))
        {
            flightCompletion.TrySetException(new InvalidOperationException("Unable to start the capture flight animation."));
        }

        return flightCompletion.Task;
    }

    public void Close()
    {
        if (window.DispatcherQueue.HasThreadAccess)
        {
            CloseCore();
        }
        else
        {
            window.DispatcherQueue.TryEnqueue(CloseCore);
        }
    }

    private Task<CaptureSelectionResult?> ShowAsync()
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
        PlatformWindowExtensions.viSetOpacity(windowHandle, 0);
        appWindow.IsShownInSwitchers = false;
        appWindow.MoveAndResize(new RectInt32(-32000, -32000, bitmap.Width, bitmap.Height));
        appWindow.Show(false);
        return completion.Task;
    }

    private void PlayFlight(DesktopCaptureBitmap capture, NativeRectangle landingBounds, Action onArrived, TaskCompletionSource<bool> flightCompletion)
    {
        if (closed)
        {
            throw new InvalidOperationException("The capture overlay is no longer available.");
        }

        if (flightInProgress)
        {
            throw new InvalidOperationException("The capture overlay is already animating.");
        }

        flightInProgress = true;
        WriteableBitmap source = CreateImageSource(capture);
        Rect sourceBounds = ToLocal(capture.Bounds);
        Rect targetBounds = ToLocal(landingBounds);
        Image image = new() { Source = source, Stretch = Stretch.Fill };
        Border captureSurface = new()
        {
            Width = Math.Max(1, sourceBounds.Width),
            Height = Math.Max(1, sourceBounds.Height),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 20, 28)),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(230, 104, 216, 255)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Child = image
        };
        Canvas.SetLeft(captureSurface, sourceBounds.X);
        Canvas.SetTop(captureSurface, sourceBounds.Y);
        flightCanvas.Children.Add(captureSurface);
        root.UpdateLayout();
        onArrived();
        root.Background = null;
        selectionChrome.Visibility = Visibility.Collapsed;

        CompositionScopedBatch? animationBatch = null;
        EventHandler<object>? renderingHandler = null;
        bool finished = false;

        void Finish(Exception? exception = null)
        {
            if (finished)
            {
                return;
            }

            finished = true;

            if (renderingHandler is not null)
            {
                CompositionTarget.Rendering -= renderingHandler;
            }

            animationBatch?.Dispose();
            animationBatch = null;
            CloseCore();

            if (exception is null)
            {
                flightCompletion.TrySetResult(true);
            }
            else
            {
                flightCompletion.TrySetException(exception);
            }
        }

        int preparationFrames = 0;
        renderingHandler = (_, _) =>
        {
            preparationFrames++;

            if (preparationFrames < 2)
            {
                return;
            }

            CompositionTarget.Rendering -= renderingHandler;
            renderingHandler = null;

            try
            {
                _ = DwmFlush();
                animationBatch = StartFlightAnimation(captureSurface, sourceBounds, targetBounds);
                animationBatch.Completed += (_, _) => Finish();
            }
            catch (Exception exception)
            {
                Finish(exception);
            }
        };
        CompositionTarget.Rendering += renderingHandler;
    }

    private static WriteableBitmap CreateImageSource(DesktopCaptureBitmap bitmap)
    {
        WriteableBitmap imageSource = new(bitmap.Width, bitmap.Height);
        using Stream stream = imageSource.PixelBuffer.AsStream();
        stream.Write(bitmap.Pixels);
        imageSource.Invalidate();
        return imageSource;
    }

    private static Brush ResolveSmokeBrush()
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("SmokeFillColorDefaultBrush", out object value) && value is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Windows.UI.Color.FromArgb(77, 0, 0, 0));
    }

    private static Task<CaptureSelectionResult?> ShowOnDispatcherAsync(DesktopCaptureBitmap bitmap, ScreenCaptureMode mode, IReadOnlyList<CaptureSelectionCandidate> candidates, ITextLocalizer localizer, DispatcherQueue dispatcherQueue)
    {
        TaskCompletionSource<CaptureSelectionResult?> result = new(TaskCreationOptions.RunContinuationsAsynchronously);

        void ShowSelectionWindow()
        {
            try
            {
                WriteableBitmap imageSource = CreateImageSource(bitmap);
                CaptureSelectionWindow selectionWindow = new(bitmap, mode, candidates, localizer, imageSource);
                _ = CompleteSelectionAsync(selectionWindow.ShowAsync(), result);
            }
            catch (Exception exception)
            {
                result.TrySetException(exception);
            }
        }

        if (dispatcherQueue.HasThreadAccess)
        {
            ShowSelectionWindow();
        }
        else if (!dispatcherQueue.TryEnqueue(ShowSelectionWindow))
        {
            result.TrySetException(new InvalidOperationException("Unable to open the capture selection window."));
        }

        return result.Task;
    }

    private static async Task CompleteSelectionAsync(Task<CaptureSelectionResult?> selection, TaskCompletionSource<CaptureSelectionResult?> result)
    {
        try
        {
            result.TrySetResult(await selection);
        }
        catch (Exception exception)
        {
            result.TrySetException(exception);
        }
    }

    private static CompositionScopedBatch StartFlightAnimation(Border captureSurface, Rect sourceBounds, Rect targetBounds)
    {
        Visual captureVisual = ElementCompositionPreview.GetElementVisual(captureSurface);
        Compositor compositor = captureVisual.Compositor;
        TimeSpan duration = TimeSpan.FromMilliseconds(AnimationDurationMs);
        SineEasingFunction captureEasing = CompositionEasingFunction.CreateSineEasingFunction(compositor, CompositionEasingFunctionMode.InOut);
        SineEasingFunction flightEasing = CompositionEasingFunction.CreateSineEasingFunction(compositor, CompositionEasingFunctionMode.Out);
        SineEasingFunction fadeEasing = CompositionEasingFunction.CreateSineEasingFunction(compositor, CompositionEasingFunctionMode.InOut);
        float captureBeatProgress = CaptureBeatDurationMs / (float)AnimationDurationMs;
        float flightStartProgress = (CaptureBeatDurationMs + CaptureHoldDurationMs) / (float)AnimationDurationMs;
        float fadeStartProgress = (AnimationDurationMs - CaptureBeatDurationMs) / (float)AnimationDurationMs;

        Vector3 sourceOffset = captureVisual.Offset;
        Vector3 sourceCenter = new((float)sourceBounds.Width / 2, (float)sourceBounds.Height / 2, 0);
        Vector3 targetCenter = new((float)(targetBounds.X + (targetBounds.Width / 2)), (float)(targetBounds.Y + (targetBounds.Height / 2)), 0);
        Vector3 targetOffset = targetCenter - sourceCenter;
        float targetScale = Math.Min(1, Math.Min(64f / Math.Max(1, (float)sourceBounds.Width), 40f / Math.Max(1, (float)sourceBounds.Height)));
        Vector3 capturedScale = new(0.965f, 0.965f, 1);
        Vector3 finalScale = new(targetScale, targetScale, 1);

        captureVisual.CenterPoint = sourceCenter;

        Vector3KeyFrameAnimation offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
        offsetAnimation.Duration = duration;
        offsetAnimation.InsertKeyFrame(0, sourceOffset);
        offsetAnimation.InsertKeyFrame(flightStartProgress, sourceOffset);
        offsetAnimation.InsertKeyFrame(1, targetOffset, flightEasing);

        Vector3KeyFrameAnimation scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.Duration = duration;
        scaleAnimation.InsertKeyFrame(0, Vector3.One);
        scaleAnimation.InsertKeyFrame(captureBeatProgress, capturedScale, captureEasing);
        scaleAnimation.InsertKeyFrame(flightStartProgress, capturedScale);
        scaleAnimation.InsertKeyFrame(1, finalScale, flightEasing);

        ScalarKeyFrameAnimation opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.Duration = duration;
        opacityAnimation.InsertKeyFrame(0, 1);
        opacityAnimation.InsertKeyFrame(fadeStartProgress, 1);
        opacityAnimation.InsertKeyFrame(1, 0, fadeEasing);

        CompositionScopedBatch batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        captureVisual.Offset = targetOffset;
        captureVisual.Scale = finalScale;
        captureVisual.Opacity = 0;
        captureVisual.StartAnimation(nameof(Visual.Offset), offsetAnimation);
        captureVisual.StartAnimation(nameof(Visual.Scale), scaleAnimation);
        captureVisual.StartAnimation(nameof(Visual.Opacity), opacityAnimation);
        batch.End();
        return batch;
    }

    private void HandleRootLoaded(object sender, RoutedEventArgs args)
    {
        root.Loaded -= HandleRootLoaded;
        root.UpdateLayout();
        UpdateSmokeBounds();
        CompositionTarget.Rendering += HandleCompositionRendering;
    }

    private void HandleRootSizeChanged(object sender, SizeChangedEventArgs args) =>
        UpdateSmokeBounds();

    private void HandleCompositionRendering(object? sender, object args)
    {
        renderedFrameCount++;

        if (closed || isShown)
        {
            CompositionTarget.Rendering -= HandleCompositionRendering;
            return;
        }

        if (!isPositioned)
        {
            isPositioned = true;
            window.AppWindow.MoveAndResize(new RectInt32(bitmap.OriginX, bitmap.OriginY, bitmap.Width, bitmap.Height));
            return;
        }

        if (renderedFrameCount < 3)
        {
            return;
        }

        CompositionTarget.Rendering -= HandleCompositionRendering;
        window.Activate();
        _ = DwmFlush();
        root.Focus(FocusState.Programmatic);
        PlatformWindowExtensions.viSetOpacity(windowHandle, 255);
        isShown = true;

        if (mode == ScreenCaptureMode.AllDisplays)
        {
            CompleteSelection(new CaptureSelectionCandidate(bitmap.Bounds));
        }
    }

    private void HandleKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Escape)
        {
            args.Handled = true;
            CancelSelection();
        }
    }

    private void HandlePointerPressed(object sender, PointerRoutedEventArgs args)
    {
        Point point = args.GetCurrentPoint(root).Position;

        if (mode == ScreenCaptureMode.Region)
        {
            selectionStart = point;
            isDragging = true;
            root.CapturePointer(args.Pointer);
            UpdateRegionHighlight(point);
            return;
        }

        CaptureSelectionCandidate? candidate = FindCandidate(point);

        if (candidate is not null)
        {
            CompleteSelection(candidate.Value);
        }
    }

    private void HandlePointerMoved(object sender, PointerRoutedEventArgs args)
    {
        Point point = args.GetCurrentPoint(root).Position;

        if (mode == ScreenCaptureMode.Region)
        {
            if (isDragging)
            {
                UpdateRegionHighlight(point);
            }

            return;
        }

        CaptureSelectionCandidate? candidate = FindCandidate(point);

        if (candidate is null)
        {
            ClearHighlight();
            return;
        }

        ShowHighlight(ToLocal(candidate.Value.Bounds));
    }

    private void HandlePointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (mode != ScreenCaptureMode.Region || !isDragging)
        {
            return;
        }

        isDragging = false;
        root.ReleasePointerCapture(args.Pointer);
        Point end = args.GetCurrentPoint(root).Position;
        Rect local = CreateRectangle(selectionStart, end);

        if (local.Width < 4 || local.Height < 4)
        {
            ClearHighlight();
            return;
        }

        CompleteSelection(new CaptureSelectionCandidate(ToScreen(local)));
    }

    private void HandleClosed(object sender, WindowEventArgs args)
    {
        closed = true;
        CompositionTarget.Rendering -= HandleCompositionRendering;
        DetachSelectionHandlers();

        if (!selectionCompleted)
        {
            selectionCompleted = true;
            completion.TrySetResult(null);
        }
    }

    private void CompleteSelection(CaptureSelectionCandidate candidate)
    {
        if (selectionCompleted)
        {
            return;
        }

        selectionCompleted = true;
        DetachSelectionHandlers();
        completion.TrySetResult(new CaptureSelectionResult(candidate, this));
    }

    private void CancelSelection()
    {
        if (selectionCompleted)
        {
            return;
        }

        selectionCompleted = true;
        completion.TrySetResult(null);
        CloseCore();
    }

    private void CloseCore()
    {
        if (closed)
        {
            return;
        }

        closed = true;
        CompositionTarget.Rendering -= HandleCompositionRendering;
        DetachSelectionHandlers();
        PlatformWindowExtensions.viSetOpacity(windowHandle, 0);
        window.Close();
    }

    private void DetachSelectionHandlers()
    {
        root.KeyDown -= HandleKeyDown;
        root.PointerMoved -= HandlePointerMoved;
        root.PointerPressed -= HandlePointerPressed;
        root.PointerReleased -= HandlePointerReleased;
    }

    private CaptureSelectionCandidate? FindCandidate(Point point)
    {
        (int x, int y) = ToScreen(point);
        CaptureSelectionCandidate candidate = candidates.FirstOrDefault(value => value.Bounds.Contains(x, y));
        return candidate.Bounds.Width > 0 ? candidate : null;
    }

    private void UpdateRegionHighlight(Point end) =>
        ShowHighlight(CreateRectangle(selectionStart, end));

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

    private void UpdateSmokeBounds() =>
        smokeBounds.Rect = new Rect(0, 0, root.ActualWidth, root.ActualHeight);

    private Rect ToLocal(NativeRectangle rectangle)
    {
        double scaleX = root.ActualWidth / bitmap.Width;
        double scaleY = root.ActualHeight / bitmap.Height;
        return new Rect((rectangle.X - bitmap.OriginX) * scaleX, (rectangle.Y - bitmap.OriginY) * scaleY, rectangle.Width * scaleX, rectangle.Height * scaleY);
    }

    private NativeRectangle ToScreen(Rect rectangle)
    {
        double scaleX = bitmap.Width / root.ActualWidth;
        double scaleY = bitmap.Height / root.ActualHeight;
        return new NativeRectangle(bitmap.OriginX + (int)Math.Round(rectangle.X * scaleX), bitmap.OriginY + (int)Math.Round(rectangle.Y * scaleY), (int)Math.Round(rectangle.Width * scaleX), (int)Math.Round(rectangle.Height * scaleY));
    }

    private (int X, int Y) ToScreen(Point point)
    {
        double scaleX = bitmap.Width / root.ActualWidth;
        double scaleY = bitmap.Height / root.ActualHeight;
        return (bitmap.OriginX + (int)Math.Round(point.X * scaleX), bitmap.OriginY + (int)Math.Round(point.Y * scaleY));
    }

    private static Rect CreateRectangle(Point start, Point end) =>
        new(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();
}

internal sealed record CaptureSelectionResult(CaptureSelectionCandidate Candidate, CaptureSelectionWindow Overlay);
