using Elysium.Platform.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics;
using WinRT.Interop;
using WinUIEx;
using PlatformWindowExtensions = Elysium.Platform.Windows.WindowExtensions;

namespace Glance.ThemeSwitcher.WinUI;

public sealed partial class ThemeTransitionService(ILogger<ThemeTransitionService> logger) :
    IDisposable
{
    private const int ExtendedWindowStyleIndex = -20;
    private const int NoActivateWindowStyle = 0x08000000;
    private const int ToolWindowStyle = 0x00000080;
    private const int TransparentWindowStyle = 0x00000020;
    private nint handle;
    private Window? window;

    public async Task PlayAsync(ThemeVariant theme,
        Func<Task> applyTheme,
        CancellationToken cancellationToken = default)
    {
        if (!GetCursorPosition(out NativePoint cursor))
        {
            await applyTheme();
            return;
        }

        PointInt32 pointer = new(cursor.X, cursor.Y);
        RectInt32 bounds = DisplayArea.GetFromPoint(pointer, DisplayAreaFallback.Nearest).OuterBounds;
        double localX = cursor.X - bounds.X;
        double localY = cursor.Y - bounds.Y;
        double radius = GetCoveringRadius(localX, localY, bounds.Width, bounds.Height) * 1.02;
        double diameter = radius * 2;
        Ellipse reveal = new()
        {
            Width = diameter,
            Height = diameter,
            Fill = new SolidColorBrush(GetTransitionColor(theme)),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(reveal, localX - radius);
        Canvas.SetTop(reveal, localY - radius);

        Canvas root = new()
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            IsHitTestVisible = false
        };
        root.Children.Add(reveal);

        Window transitionWindow = GetWindow(root, bounds);
        DispatcherQueue dispatcherQueue = transitionWindow.DispatcherQueue;
        Visual revealVisual = ElementCompositionPreview.GetElementVisual(reveal);
        revealVisual.CenterPoint = new Vector3((float)radius, (float)radius, 0);
        revealVisual.Scale = new Vector3(0.01f);
        Visual rootVisual = ElementCompositionPreview.GetElementVisual(root);
        rootVisual.Opacity = 1;
        bool shown = false;
        bool themeApplied = false;

        try
        {
            shown = true;
            await ShowPreparedAsync(transitionWindow, root, bounds, handle, cancellationToken);
            await RunOnDispatcherAsync(dispatcherQueue, () => StartRevealAnimation(revealVisual));
            await Task.Delay(260, cancellationToken);
            await applyTheme().ConfigureAwait(false);
            themeApplied = true;
            await WaitForRenderingFramesAsync(dispatcherQueue, 2, cancellationToken);
            await RunOnDispatcherAsync(dispatcherQueue, () => StartFadeAnimation(rootVisual));
            await Task.Delay(120, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Unable to complete the {Theme} theme transition", theme);

            if (!themeApplied)
            {
                await applyTheme();
            }
        }
        finally
        {
            await RunOnDispatcherAsync(dispatcherQueue, () =>
            {
                revealVisual.StopAnimation(nameof(Visual.Scale));
                rootVisual.StopAnimation(nameof(Visual.Opacity));
                PlatformWindowExtensions.viSetOpacity(handle, 0);

                if (shown)
                {
                    transitionWindow.AppWindow.Hide();
                }
            });
        }
    }

    public void Dispose()
    {
        if (window is null)
        {
            return;
        }

        Window closingWindow = window;
        nint closingHandle = handle;
        window = null;
        handle = 0;

        void Close()
        {
            PlatformWindowExtensions.viSetOpacity(closingHandle, 0);
            closingWindow.Close();
        }

        if (closingWindow.DispatcherQueue.HasThreadAccess)
        {
            Close();
        }
        else
        {
            _ = closingWindow.DispatcherQueue.TryEnqueue(Close);
        }
    }

    private Window GetWindow(UIElement content,
        RectInt32 bounds)
    {
        if (window is null)
        {
            window = new Window
            {
                ExtendsContentIntoTitleBar = true,
                SystemBackdrop = new TransparentTintBackdrop()
            };
            window.SetTitleBar(null);
            window.AppWindow.IsShownInSwitchers = false;
            handle = WindowNative.GetWindowHandle(window);

            if (window.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = true;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsResizable = false;
                presenter.SetBorderAndTitleBar(false, false);
            }

            PlatformWindowExtensions.SetBorderless(handle, true);
            PlatformWindowExtensions.SetCornerRadius(handle, WindowCornerPreference.Sharp);
            PlatformWindowExtensions.SetTopMost(handle, true);
            PlatformWindowExtensions.viSetOpacity(handle, 0);

            int extendedStyle = GetWindowLong(handle, ExtendedWindowStyleIndex);
            _ = SetWindowLong(handle, ExtendedWindowStyleIndex, extendedStyle | TransparentWindowStyle | ToolWindowStyle | NoActivateWindowStyle);
        }

        window.Content = content;
        window.AppWindow.MoveAndResize(new RectInt32(-32000, -32000, bounds.Width, bounds.Height));
        return window;
    }

    private static void StartRevealAnimation(Visual visual)
    {
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1), new Vector2(0.3f, 1));
        Vector3KeyFrameAnimation animation = compositor.CreateVector3KeyFrameAnimation();
        animation.InsertKeyFrame(0, new Vector3(0.01f), easing);
        animation.InsertKeyFrame(1, Vector3.One, easing);
        animation.Duration = TimeSpan.FromMilliseconds(260);
        visual.Scale = Vector3.One;
        visual.StartAnimation(nameof(Visual.Scale), animation);
    }

    private static void StartFadeAnimation(Visual visual)
    {
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0), new Vector2(1, 1));
        ScalarKeyFrameAnimation animation = compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(0, 1, easing);
        animation.InsertKeyFrame(1, 0, easing);
        animation.Duration = TimeSpan.FromMilliseconds(120);
        visual.Opacity = 0;
        visual.StartAnimation(nameof(Visual.Opacity), animation);
    }

    private static Windows.UI.Color GetTransitionColor(ThemeVariant theme) => theme == ThemeVariant.Light
        ? Windows.UI.Color.FromArgb(255, 243, 243, 243)
        : Windows.UI.Color.FromArgb(255, 32, 32, 32);

    private static double GetCoveringRadius(double x,
        double y,
        double width,
        double height)
    {
        double horizontal = Math.Max(x, width - x);
        double vertical = Math.Max(y, height - y);
        return Math.Sqrt(horizontal * horizontal + vertical * vertical);
    }

    private static Task RunOnDispatcherAsync(DispatcherQueue dispatcherQueue,
        Action action)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
                completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }))
        {
            completion.TrySetException(new InvalidOperationException("The theme transition dispatcher is unavailable."));
        }

        return completion.Task;
    }

    private static async Task WaitForRenderingFramesAsync(DispatcherQueue dispatcherQueue,
        int count,
        CancellationToken cancellationToken)
    {
        int renderedFrameCount = 0;
        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        await RunOnDispatcherAsync(dispatcherQueue, () => CompositionTarget.Rendering += HandleRendering);

        try
        {
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        }
        finally
        {
            await RunOnDispatcherAsync(dispatcherQueue, () => CompositionTarget.Rendering -= HandleRendering);
        }

        void HandleRendering(object? sender, object args)
        {
            renderedFrameCount++;

            if (renderedFrameCount >= count)
            {
                completion.TrySetResult(true);
            }
        }
    }

    private static async Task ShowPreparedAsync(Window window,
        FrameworkElement root,
        RectInt32 bounds,
        nint handle,
        CancellationToken cancellationToken)
    {
        DispatcherQueue dispatcherQueue = window.DispatcherQueue;
        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        int renderedFrameCount = 0;
        bool isPositioned = false;
        bool isRendering = false;

        await RunOnDispatcherAsync(dispatcherQueue, () =>
        {
            root.Loaded += HandleLoaded;

            if (root.IsLoaded)
            {
                StartRendering();
            }

            window.AppWindow.Show(false);
        });

        try
        {
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        }
        finally
        {
            await RunOnDispatcherAsync(dispatcherQueue, () =>
            {
                root.Loaded -= HandleLoaded;
                CompositionTarget.Rendering -= HandleRendering;
            });
        }

        void HandleLoaded(object sender, RoutedEventArgs args)
        {
            StartRendering();
        }

        void StartRendering()
        {
            if (isRendering)
            {
                return;
            }

            isRendering = true;
            root.UpdateLayout();
            CompositionTarget.Rendering += HandleRendering;
        }

        void HandleRendering(object? sender, object args)
        {
            renderedFrameCount++;

            if (!isPositioned)
            {
                isPositioned = true;
                window.AppWindow.MoveAndResize(bounds);
                return;
            }

            if (renderedFrameCount < 3)
            {
                return;
            }

            CompositionTarget.Rendering -= HandleRendering;
            _ = DwmFlush();
            PlatformWindowExtensions.viSetOpacity(handle, 255);
            completion.TrySetResult(true);
        }
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmFlush();

    [LibraryImport("user32.dll", EntryPoint = "GetCursorPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPosition(out NativePoint point);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static partial int GetWindowLong(nint window,
        int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static partial int SetWindowLong(nint window,
        int index,
        int value);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;

        public int Y;
    }
}
