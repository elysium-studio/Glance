using Elysium.Platform.Windows;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics;
using WinRT.Interop;
using WinUIEx;
using PlatformWindowExtensions = Elysium.Platform.Windows.WindowExtensions;

namespace Glance.ScreenCapture.WinUI;

internal sealed class CaptureFlightWindow
{
    private const int AnimationDurationMs = 720;
    private const int ExtendedWindowStyleIndex = -20;
    private const int NoActivateExtendedStyle = 0x08000000;
    private const int ToolWindowExtendedStyle = 0x00000080;
    private const int TransparentExtendedStyle = 0x00000020;

    private readonly Border captureSurface;
    private readonly CaptureAnimationFrame frame;
    private readonly Border landingPulse;
    private readonly NativeRectangle landingBounds;
    private readonly Canvas root;
    private readonly Window window;
    private readonly nint windowHandle;

    private CaptureFlightWindow(CaptureAnimationFrame frame, NativeRectangle landingBounds)
    {
        this.frame = frame;
        this.landingBounds = landingBounds;

        WriteableBitmap source = CreateImageSource(frame.Bitmap);
        Image image = new()
        {
            Source = source,
            Stretch = Stretch.Fill
        };
        captureSurface = new Border
        {
            Width = frame.Bitmap.Width,
            Height = frame.Bitmap.Height,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 20, 28)),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(230, 104, 216, 255)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Child = image
        };
        landingPulse = new Border
        {
            Width = 64,
            Height = 40,
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 104, 216, 255)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(12),
            IsHitTestVisible = false,
            Opacity = 0
        };
        root = new Canvas
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            IsHitTestVisible = false
        };
        root.Children.Add(landingPulse);
        root.Children.Add(captureSurface);

        NativeRectangle desktop = frame.DesktopBounds;
        Canvas.SetLeft(captureSurface, frame.Bitmap.OriginX - desktop.X);
        Canvas.SetTop(captureSurface, frame.Bitmap.OriginY - desktop.Y);
        Canvas.SetLeft(landingPulse, landingBounds.X - desktop.X + ((landingBounds.Width - landingPulse.Width) / 2));
        Canvas.SetTop(landingPulse, landingBounds.Y - desktop.Y + ((landingBounds.Height - landingPulse.Height) / 2));

        window = new Window
        {
            Content = root,
            ExtendsContentIntoTitleBar = true,
            SystemBackdrop = new TransparentTintBackdrop()
        };
        window.SetTitleBar(null);
        windowHandle = WindowNative.GetWindowHandle(window);
    }

    public static Task PlayAsync(CaptureAnimationFrame frame, NativeRectangle landingBounds, DispatcherQueue dispatcherQueue)
    {
        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        async void Play()
        {
            try
            {
                CaptureFlightWindow flightWindow = new(frame, landingBounds);
                await flightWindow.PlayAsync();
                completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        if (dispatcherQueue.HasThreadAccess)
        {
            Play();
        }
        else if (!dispatcherQueue.TryEnqueue(Play))
        {
            completion.TrySetException(new InvalidOperationException("Unable to start the capture flight animation."));
        }

        return completion.Task;
    }

    private Task PlayAsync()
    {
        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DispatcherQueueTimer? closeTimer = null;
        EventHandler<object>? renderingHandler = null;
        bool completed = false;

        void Complete(Exception? exception = null)
        {
            if (completed)
            {
                return;
            }

            completed = true;

            if (renderingHandler is not null)
            {
                CompositionTarget.Rendering -= renderingHandler;
            }

            closeTimer?.Stop();

            try
            {
                PlatformWindowExtensions.viSetOpacity(windowHandle, 0);
                window.Close();
            }
            catch (Exception closeException)
            {
                exception ??= closeException;
            }

            if (exception is null)
            {
                completion.TrySetResult(true);
            }
            else
            {
                completion.TrySetException(exception);
            }
        }

        void HandleLoaded(object sender, RoutedEventArgs args)
        {
            root.Loaded -= HandleLoaded;

            try
            {
                root.UpdateLayout();
                int renderedFrames = 0;
                renderingHandler = (_, _) =>
                {
                    renderedFrames++;

                    if (renderedFrames < 2)
                    {
                        return;
                    }

                    CompositionTarget.Rendering -= renderingHandler;
                    renderingHandler = null;

                    try
                    {
                        _ = DwmFlush();
                        PlatformWindowExtensions.viSetOpacity(windowHandle, 255);
                        StartFlightAnimation();
                        closeTimer = window.DispatcherQueue.CreateTimer();
                        closeTimer.Interval = TimeSpan.FromMilliseconds(AnimationDurationMs + 40);
                        closeTimer.IsRepeating = false;
                        closeTimer.Tick += (_, _) => Complete();
                        closeTimer.Start();
                    }
                    catch (Exception exception)
                    {
                        Complete(exception);
                    }
                };
                CompositionTarget.Rendering += renderingHandler;
            }
            catch (Exception exception)
            {
                Complete(exception);
            }
        }

        try
        {
            ConfigureWindow();
            root.Loaded += HandleLoaded;
            window.AppWindow.Show(false);
        }
        catch (Exception exception)
        {
            root.Loaded -= HandleLoaded;
            Complete(exception);
        }

        return completion.Task;
    }

    private void ConfigureWindow()
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
        int extendedStyle = GetWindowLong(windowHandle, ExtendedWindowStyleIndex);
        _ = SetWindowLong(windowHandle, ExtendedWindowStyleIndex, extendedStyle | NoActivateExtendedStyle | ToolWindowExtendedStyle | TransparentExtendedStyle);
        appWindow.IsShownInSwitchers = false;
        appWindow.MoveAndResize(new RectInt32(frame.DesktopBounds.X, frame.DesktopBounds.Y, frame.DesktopBounds.Width, frame.DesktopBounds.Height));
    }

    private void StartFlightAnimation()
    {
        Visual captureVisual = ElementCompositionPreview.GetElementVisual(captureSurface);
        Visual pulseVisual = ElementCompositionPreview.GetElementVisual(landingPulse);
        Compositor compositor = captureVisual.Compositor;
        TimeSpan duration = TimeSpan.FromMilliseconds(AnimationDurationMs);
        CubicBezierEasingFunction accelerate = compositor.CreateCubicBezierEasingFunction(new Vector2(0.72f, 0), new Vector2(0.88f, 0.42f));
        CubicBezierEasingFunction settle = compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1), new Vector2(0.3f, 1));

        Vector3 sourceOffset = captureVisual.Offset;
        Vector3 sourceCenter = new((float)captureSurface.ActualWidth / 2, (float)captureSurface.ActualHeight / 2, 0);
        Vector3 targetCenter = new(
            landingBounds.X - frame.DesktopBounds.X + (landingBounds.Width / 2f),
            landingBounds.Y - frame.DesktopBounds.Y + (landingBounds.Height / 2f),
            0);
        Vector3 targetOffset = targetCenter - sourceCenter;
        float targetScale = Math.Min(1, Math.Min(64f / Math.Max(1, frame.Bitmap.Width), 40f / Math.Max(1, frame.Bitmap.Height)));
        Vector3 finalScale = new(targetScale, targetScale, 1);

        captureVisual.CenterPoint = sourceCenter;

        Vector3KeyFrameAnimation offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
        offsetAnimation.Duration = duration;
        offsetAnimation.InsertKeyFrame(0, sourceOffset);
        offsetAnimation.InsertKeyFrame(0.16f, Vector3.Lerp(sourceOffset, targetOffset, 0.03f), settle);
        offsetAnimation.InsertKeyFrame(0.76f, Vector3.Lerp(sourceOffset, targetOffset, 0.82f), accelerate);
        offsetAnimation.InsertKeyFrame(1, targetOffset, accelerate);

        Vector3KeyFrameAnimation scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.Duration = duration;
        scaleAnimation.InsertKeyFrame(0, Vector3.One);
        scaleAnimation.InsertKeyFrame(0.12f, new Vector3(1.015f, 1.015f, 1), settle);
        scaleAnimation.InsertKeyFrame(0.72f, Vector3.Lerp(Vector3.One, finalScale, 0.64f), accelerate);
        scaleAnimation.InsertKeyFrame(1, finalScale, accelerate);

        ScalarKeyFrameAnimation opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.Duration = duration;
        opacityAnimation.InsertKeyFrame(0, 1);
        opacityAnimation.InsertKeyFrame(0.72f, 0.98f);
        opacityAnimation.InsertKeyFrame(0.92f, 0.62f, accelerate);
        opacityAnimation.InsertKeyFrame(1, 0);

        pulseVisual.CenterPoint = new Vector3((float)landingPulse.ActualWidth / 2, (float)landingPulse.ActualHeight / 2, 0);
        Vector3KeyFrameAnimation pulseScaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        pulseScaleAnimation.Duration = duration;
        pulseScaleAnimation.InsertKeyFrame(0.72f, new Vector3(0.7f, 0.7f, 1));
        pulseScaleAnimation.InsertKeyFrame(1, new Vector3(1.2f, 1.2f, 1), settle);
        ScalarKeyFrameAnimation pulseOpacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        pulseOpacityAnimation.Duration = duration;
        pulseOpacityAnimation.InsertKeyFrame(0.72f, 0);
        pulseOpacityAnimation.InsertKeyFrame(0.88f, 0.82f, settle);
        pulseOpacityAnimation.InsertKeyFrame(1, 0);

        captureVisual.StartAnimation(nameof(Visual.Offset), offsetAnimation);
        captureVisual.StartAnimation(nameof(Visual.Scale), scaleAnimation);
        captureVisual.StartAnimation(nameof(Visual.Opacity), opacityAnimation);
        pulseVisual.StartAnimation(nameof(Visual.Scale), pulseScaleAnimation);
        pulseVisual.StartAnimation(nameof(Visual.Opacity), pulseOpacityAnimation);
    }

    private static WriteableBitmap CreateImageSource(DesktopCaptureBitmap bitmap)
    {
        WriteableBitmap imageSource = new(bitmap.Width, bitmap.Height);
        using Stream stream = imageSource.PixelBuffer.AsStream();
        stream.Write(bitmap.Pixels);
        imageSource.Invalidate();
        return imageSource;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(nint window, int index, int value);

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();
}
