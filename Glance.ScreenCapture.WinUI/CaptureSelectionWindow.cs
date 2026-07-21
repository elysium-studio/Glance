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
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Glance.ScreenCapture.WinUI;

internal sealed class CaptureSelectionWindow
{
    private readonly DesktopCaptureBitmap bitmap;
    private readonly IReadOnlyList<NativeRectangle> candidates;
    private readonly TaskCompletionSource<NativeRectangle?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Border highlight;
    private readonly ScreenCaptureMode mode;
    private readonly Grid root;
    private readonly Window window;
    private bool completed;
    private bool isDragging;
    private Point selectionStart;

    private CaptureSelectionWindow(
        DesktopCaptureBitmap bitmap,
        ScreenCaptureMode mode,
        IReadOnlyList<NativeRectangle> candidates,
        ITextLocalizer localizer,
        ImageSource imageSource)
    {
        this.bitmap = bitmap;
        this.mode = mode;
        this.candidates = candidates;

        Image image = new()
        {
            Source = imageSource,
            Stretch = Stretch.Fill
        };
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
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0)),
            IsTabStop = true
        };
        root.Children.Add(image);
        root.Children.Add(selectionCanvas);
        root.Children.Add(instructionContainer);
        root.KeyDown += HandleKeyDown;
        root.PointerMoved += HandlePointerMoved;
        root.PointerPressed += HandlePointerPressed;
        root.PointerReleased += HandlePointerReleased;
        root.Loaded += (_, _) => root.Focus(FocusState.Programmatic);

        window = new Window
        {
            Content = root,
            ExtendsContentIntoTitleBar = true
        };
        window.Closed += HandleClosed;
    }

    public static async Task<NativeRectangle?> SelectAsync(
        DesktopCaptureBitmap bitmap,
        ScreenCaptureMode mode,
        IReadOnlyList<NativeRectangle> candidates,
        ITextLocalizer localizer,
        DispatcherQueue dispatcherQueue)
    {
        using InMemoryRandomAccessStream stream = await CreateImageStreamAsync(bitmap);
        return await ShowOnDispatcherAsync(bitmap, mode, candidates, localizer, dispatcherQueue, stream);
    }

    private Task<NativeRectangle?> ShowAsync()
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

        appWindow.IsShownInSwitchers = false;
        appWindow.MoveAndResize(new RectInt32(bitmap.OriginX, bitmap.OriginY, bitmap.Width, bitmap.Height));
        window.Activate();
        return completion.Task;
    }

    private static async Task<InMemoryRandomAccessStream> CreateImageStreamAsync(DesktopCaptureBitmap bitmap)
    {
        InMemoryRandomAccessStream stream = new();
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)bitmap.Width, (uint)bitmap.Height, 96, 96, bitmap.Pixels);
        await encoder.FlushAsync();
        stream.Seek(0);
        return stream;
    }

    private static Task<NativeRectangle?> ShowOnDispatcherAsync(DesktopCaptureBitmap bitmap, ScreenCaptureMode mode, IReadOnlyList<NativeRectangle> candidates, ITextLocalizer localizer, DispatcherQueue dispatcherQueue, IRandomAccessStream stream)
    {
        TaskCompletionSource<NativeRectangle?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        void ShowSelectionWindow()
        {
            try
            {
                BitmapImage imageSource = new();
                imageSource.SetSource(stream);
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

    private static async Task CompleteSelectionAsync(Task<NativeRectangle?> selection, TaskCompletionSource<NativeRectangle?> completion)
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

        NativeRectangle? candidate = FindCandidate(point);

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

        NativeRectangle? candidate = FindCandidate(point);

        if (candidate is null)
        {
            highlight.Visibility = Visibility.Collapsed;
            return;
        }

        ShowHighlight(ToLocal(candidate.Value));
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

        Complete(ToScreen(local));
    }

    private void HandleClosed(object sender, WindowEventArgs args)
    {
        if (!completed)
        {
            completed = true;
            completion.TrySetResult(null);
        }
    }

    private NativeRectangle? FindCandidate(Point point)
    {
        (int x, int y) = ToScreen(point);
        return candidates.FirstOrDefault(candidate => candidate.Contains(x, y)) is NativeRectangle rectangle && rectangle.Width > 0
            ? rectangle
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

    private void Complete(NativeRectangle? rectangle)
    {
        if (completed)
        {
            return;
        }

        completed = true;
        completion.TrySetResult(rectangle);
        window.Close();
    }
}
