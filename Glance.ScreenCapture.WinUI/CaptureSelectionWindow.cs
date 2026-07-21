using Elysium.Platform.Windows;
using Glance.Application.Abstractions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private readonly DesktopCaptureBitmap bitmap;
    private readonly IReadOnlyList<CaptureSelectionCandidate> candidates;
    private readonly TaskCompletionSource<CaptureSelectionCandidate?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Border highlight;
    private readonly nint windowHandle;
    private readonly ScreenCaptureMode mode;
    private readonly Grid root;
    private readonly Window window;
    private bool completed;
    private bool isDragging;
    private bool isPositioned;
    private bool isShown;
    private int renderedFrameCount;
    private Point selectionStart;

    private CaptureSelectionWindow(
        DesktopCaptureBitmap bitmap,
        ScreenCaptureMode mode,
        IReadOnlyList<CaptureSelectionCandidate> candidates,
        ITextLocalizer localizer,
        ImageSource imageSource)
    {
        this.bitmap = bitmap;
        this.mode = mode;
        this.candidates = candidates;

        highlight = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(24, 104, 216, 255)),
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
        root = new Grid
        {
            Background = new ImageBrush { ImageSource = imageSource, Stretch = Stretch.Fill },
            IsTabStop = true
        };
        root.Children.Add(selectionCanvas);
        root.Children.Add(instructionContainer);
        root.KeyDown += HandleKeyDown;
        root.PointerMoved += HandlePointerMoved;
        root.PointerPressed += HandlePointerPressed;
        root.PointerReleased += HandlePointerReleased;
        root.Loaded += HandleRootLoaded;

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

    public static Task<CaptureSelectionCandidate?> SelectAsync(
        DesktopCaptureBitmap bitmap,
        ScreenCaptureMode mode,
        IReadOnlyList<CaptureSelectionCandidate> candidates,
        ITextLocalizer localizer,
        DispatcherQueue dispatcherQueue) =>
        ShowOnDispatcherAsync(bitmap, mode, candidates, localizer, dispatcherQueue);

    private Task<CaptureSelectionCandidate?> ShowAsync()
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

    private static WriteableBitmap CreateImageSource(DesktopCaptureBitmap bitmap)
    {
        WriteableBitmap imageSource = new(bitmap.Width, bitmap.Height);
        using Stream stream = imageSource.PixelBuffer.AsStream();
        stream.Write(bitmap.Pixels);
        imageSource.Invalidate();
        return imageSource;
    }

    private static Task<CaptureSelectionCandidate?> ShowOnDispatcherAsync(DesktopCaptureBitmap bitmap, ScreenCaptureMode mode, IReadOnlyList<CaptureSelectionCandidate> candidates, ITextLocalizer localizer, DispatcherQueue dispatcherQueue)
    {
        TaskCompletionSource<CaptureSelectionCandidate?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        void ShowSelectionWindow()
        {
            try
            {
                WriteableBitmap imageSource = CreateImageSource(bitmap);
                CaptureSelectionWindow selectionWindow = new(bitmap, mode, candidates, localizer, imageSource);
                _ = CompleteSelectionAsync(selectionWindow.ShowAsync(), completion);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        if (dispatcherQueue.HasThreadAccess)
        {
            ShowSelectionWindow();
        }
        else if (!dispatcherQueue.TryEnqueue(ShowSelectionWindow))
        {
            completion.TrySetException(new InvalidOperationException("Unable to open the capture selection window."));
        }

        return completion.Task;
    }

    private void HandleRootLoaded(object sender, RoutedEventArgs args)
    {
        root.Loaded -= HandleRootLoaded;
        root.UpdateLayout();
        CompositionTarget.Rendering += HandleCompositionRendering;
    }

    private void HandleCompositionRendering(object? sender, object args)
    {
        renderedFrameCount++;

        if (completed || isShown)
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
    }

    private static async Task CompleteSelectionAsync(Task<CaptureSelectionCandidate?> selection, TaskCompletionSource<CaptureSelectionCandidate?> completion)
    {
        try
        {
            completion.TrySetResult(await selection);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private void HandleKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Escape)
        {
            args.Handled = true;
            Complete(null);
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
            Complete(candidate);
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
            highlight.Visibility = Visibility.Collapsed;
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
            highlight.Visibility = Visibility.Collapsed;
            return;
        }

        Complete(new CaptureSelectionCandidate(ToScreen(local)));
    }

    private void HandleClosed(object sender, WindowEventArgs args)
    {
        CompositionTarget.Rendering -= HandleCompositionRendering;

        if (!completed)
        {
            completed = true;
            completion.TrySetResult(null);
        }
    }

    private CaptureSelectionCandidate? FindCandidate(Point point)
    {
        (int x, int y) = ToScreen(point);
        CaptureSelectionCandidate candidate = candidates.FirstOrDefault(value => value.Bounds.Contains(x, y));
        return candidate.Bounds.Width > 0
            ? candidate
            : null;
    }

    private void UpdateRegionHighlight(Point end)
    {
        Rect rectangle = CreateRectangle(selectionStart, end);
        ShowHighlight(rectangle);
    }

    private void ShowHighlight(Rect rectangle)
    {
        Canvas.SetLeft(highlight, rectangle.X);
        Canvas.SetTop(highlight, rectangle.Y);
        highlight.Width = rectangle.Width;
        highlight.Height = rectangle.Height;
        highlight.Visibility = Visibility.Visible;
    }

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

    private void Complete(CaptureSelectionCandidate? candidate)
    {
        if (completed)
        {
            return;
        }

        completed = true;
        CompositionTarget.Rendering -= HandleCompositionRendering;
        PlatformWindowExtensions.viSetOpacity(windowHandle, 0);
        completion.TrySetResult(candidate);
        window.Close();
    }
}
