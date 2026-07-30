using Elysium.Platform.Windows;
using Glance.Application.Abstractions;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
using WinRT;
using WinRT.Interop;
using WinUIEx;
using PlatformWindowExtensions = Elysium.Platform.Windows.WindowExtensions;

namespace Glance.ScreenLens.WinUI;

internal sealed class LensSelectionWindow
{
    private readonly LensBitmap bitmap;
    private readonly TaskCompletionSource<LensRectangle?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Border highlight;
    private readonly Grid root;
    private readonly RectangleGeometry smokeBounds;
    private readonly RectangleGeometry smokeCutout;
    private readonly Window window;
    private readonly nint windowHandle;
    private bool closed;
    private bool isDragging;
    private Point selectionStart;

    private LensSelectionWindow(LensBitmap bitmap, ITextLocalizer localizer)
    {
        this.bitmap = bitmap;
        smokeBounds = new RectangleGeometry();
        smokeCutout = new RectangleGeometry();
        GeometryGroup smokeGeometry = new() { FillRule = FillRule.EvenOdd };
        smokeGeometry.Children.Add(smokeBounds);
        smokeGeometry.Children.Add(smokeCutout);

        Microsoft.UI.Xaml.Shapes.Path smokeOverlay = new()
        {
            Data = smokeGeometry,
            Fill = ResolveSmokeBrush()
        };

        highlight = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 85, 214, 190)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            Visibility = Visibility.Collapsed
        };

        Canvas selectionCanvas = new();
        selectionCanvas.Children.Add(highlight);

        TextBlock instruction = new()
        {
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
            Text = localizer.GetText("SelectionInstruction")
        };

        Border instructionContainer = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 24, 0, 0),
            Padding = new Thickness(14, 8, 14, 8),
            Background = new SolidColorBrush(Color.FromArgb(220, 24, 24, 24)),
            CornerRadius = new CornerRadius(8),
            Child = instruction
        };

        root = new Grid
        {
            Background = new ImageBrush { ImageSource = CreateImageSource(bitmap), Stretch = Stretch.Fill },
            IsTabStop = true
        };
        root.Children.Add(smokeOverlay);
        root.Children.Add(selectionCanvas);
        root.Children.Add(instructionContainer);
        root.KeyDown += HandleKeyDown;
        root.PointerMoved += HandlePointerMoved;
        root.PointerPressed += HandlePointerPressed;
        root.PointerReleased += HandlePointerReleased;
        root.Loaded += HandleLoaded;
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

    public static Task<LensRectangle?> SelectAsync(LensBitmap bitmap, ITextLocalizer localizer)
    {
        LensSelectionWindow selectionWindow = new(bitmap, localizer);
        return selectionWindow.ShowAsync();
    }

    private static WriteableBitmap CreateImageSource(LensBitmap bitmap)
    {
        WriteableBitmap imageSource = new(bitmap.Width, bitmap.Height);
        using Stream stream = imageSource.PixelBuffer.AsStream();
        stream.Write(bitmap.Pixels);
        imageSource.Invalidate();
        return imageSource;
    }

    private static Rect CreateRectangle(Point start, Point end) =>
        new(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));

    private static Brush ResolveSmokeBrush()
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("SmokeFillColorDefaultBrush", out object value) && value is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Color.FromArgb(77, 0, 0, 0));
    }

    private void Cancel() => Complete(null);

    private void Complete(LensRectangle? rectangle)
    {
        if (closed)
        {
            return;
        }

        closed = true;
        root.ReleasePointerCaptures();
        completion.TrySetResult(rectangle);
        window.Close();
    }

    private void HandleClosed(object sender, WindowEventArgs args)
    {
        root.KeyDown -= HandleKeyDown;
        root.PointerMoved -= HandlePointerMoved;
        root.PointerPressed -= HandlePointerPressed;
        root.PointerReleased -= HandlePointerReleased;
        root.SizeChanged -= HandleSizeChanged;

        if (!closed)
        {
            closed = true;
            completion.TrySetResult(null);
        }
    }

    private void HandleKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Escape)
        {
            args.Handled = true;
            Cancel();
        }
    }

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        root.Loaded -= HandleLoaded;
        root.UpdateLayout();
        UpdateSmokeBounds();
        window.Activate();
        root.Focus(FocusState.Programmatic);
    }

    private void HandlePointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (isDragging)
        {
            ShowHighlight(CreateRectangle(selectionStart, args.GetCurrentPoint(root).Position));
        }
    }

    private void HandlePointerPressed(object sender, PointerRoutedEventArgs args)
    {
        Microsoft.UI.Input.PointerPoint point = args.GetCurrentPoint(root);

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        selectionStart = point.Position;
        isDragging = true;
        root.CapturePointer(args.Pointer);
        ShowHighlight(CreateRectangle(selectionStart, selectionStart));
        args.Handled = true;
    }

    private void HandlePointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        root.ReleasePointerCapture(args.Pointer);
        Rect local = CreateRectangle(selectionStart, args.GetCurrentPoint(root).Position);
        args.Handled = true;

        if (local.Width < 4 || local.Height < 4)
        {
            highlight.Visibility = Visibility.Collapsed;
            smokeCutout.Rect = default;
            return;
        }

        Complete(ToScreen(local));
    }

    private void HandleSizeChanged(object sender, SizeChangedEventArgs args) =>
        UpdateSmokeBounds();

    private Task<LensRectangle?> ShowAsync()
    {
        OverlappedPresenter presenter = window.AppWindow.Presenter.As<OverlappedPresenter>();
        presenter.IsAlwaysOnTop = true;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsResizable = false;
        presenter.SetBorderAndTitleBar(false, false);
        PlatformWindowExtensions.SetBorderless(windowHandle, true);
        PlatformWindowExtensions.SetCornerRadius(windowHandle, WindowCornerPreference.Sharp);
        PlatformWindowExtensions.SetTopMost(windowHandle, true);
        window.AppWindow.IsShownInSwitchers = false;
        window.AppWindow.MoveAndResize(new RectInt32(bitmap.OriginX, bitmap.OriginY, bitmap.Width, bitmap.Height));
        window.AppWindow.Show(false);
        return completion.Task;
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

    private LensRectangle ToScreen(Rect rectangle)
    {
        double scaleX = bitmap.Width / root.ActualWidth;
        double scaleY = bitmap.Height / root.ActualHeight;
        return new LensRectangle(bitmap.OriginX + (int)Math.Round(rectangle.X * scaleX),
            bitmap.OriginY + (int)Math.Round(rectangle.Y * scaleY),
            (int)Math.Round(rectangle.Width * scaleX),
            (int)Math.Round(rectangle.Height * scaleY));
    }

    private void UpdateSmokeBounds() =>
        smokeBounds.Rect = new Rect(0, 0, root.ActualWidth, root.ActualHeight);
}
