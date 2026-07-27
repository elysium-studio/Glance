using Elysium.Platform.Windows;
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

public sealed partial class ThemeTransitionService :
    IDisposable
{
    private const int ExtendedWindowStyleIndex = -20;
    private const int NoActivateWindowStyle = 0x08000000;
    private const int ShowWindowHidden = 0;
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
        double radius = GetCoveringRadius(localX, localY, bounds.Width, bounds.Height);
        double diameter = radius * 2;

        Ellipse wash = new()
        {
            Width = diameter,
            Height = diameter,
            Fill = new SolidColorBrush(theme == ThemeVariant.Light
                ? Windows.UI.Color.FromArgb(255, 246, 248, 252)
                : Windows.UI.Color.FromArgb(255, 24, 26, 34)),
            Stroke = new SolidColorBrush(theme == ThemeVariant.Light
                ? Windows.UI.Color.FromArgb(255, 255, 190, 92)
                : Windows.UI.Color.FromArgb(255, 126, 108, 255)),
            StrokeThickness = 3,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(wash, localX - radius);
        Canvas.SetTop(wash, localY - radius);

        Canvas root = new()
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0)),
            IsHitTestVisible = false
        };
        root.Children.Add(wash);

        Window transitionWindow = GetWindow(root, bounds);
        Visual washVisual = ElementCompositionPreview.GetElementVisual(wash);
        washVisual.CenterPoint = new Vector3((float)radius, (float)radius, 0);
        washVisual.Scale = new Vector3(0.015f);
        Visual rootVisual = ElementCompositionPreview.GetElementVisual(root);
        rootVisual.Opacity = 1;
        DispatcherQueue dispatcherQueue = transitionWindow.DispatcherQueue;

        TaskCompletionSource<bool> loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        root.Loaded += HandleLoaded;
        bool shown = false;

        try
        {
            transitionWindow.AppWindow.Show(false);
            shown = true;
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            await RunOnDispatcherAsync(dispatcherQueue, root.UpdateLayout);
            await WaitForRenderingFramesAsync(dispatcherQueue, 2, cancellationToken);
            await RunOnDispatcherAsync(dispatcherQueue, () => transitionWindow.AppWindow.MoveAndResize(bounds));
            await WaitForRenderingFramesAsync(dispatcherQueue, 2, cancellationToken);
            await RunOnDispatcherAsync(dispatcherQueue, () =>
            {
                _ = DwmFlush();
                PlatformWindowExtensions.viSetOpacity(handle, 255);
                Compositor compositor = washVisual.Compositor;
                CubicBezierEasingFunction revealEasing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1), new Vector2(0.3f, 1));
                Vector3KeyFrameAnimation reveal = compositor.CreateVector3KeyFrameAnimation();
                reveal.InsertKeyFrame(0, new Vector3(0.015f), revealEasing);
                reveal.InsertKeyFrame(1, Vector3.One, revealEasing);
                reveal.Duration = TimeSpan.FromMilliseconds(300);
                washVisual.Scale = Vector3.One;
                washVisual.StartAnimation(nameof(Visual.Scale), reveal);
            });

            await Task.Delay(145, cancellationToken);
            await applyTheme().ConfigureAwait(false);
            await Task.Delay(155, cancellationToken);
            await RunOnDispatcherAsync(dispatcherQueue, () =>
            {
                Compositor compositor = rootVisual.Compositor;
                ScalarKeyFrameAnimation fade = compositor.CreateScalarKeyFrameAnimation();
                fade.InsertKeyFrame(0, 1);
                fade.InsertKeyFrame(1, 0, compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0), new Vector2(1, 1)));
                fade.Duration = TimeSpan.FromMilliseconds(150);
                rootVisual.Opacity = 0;
                rootVisual.StartAnimation(nameof(Visual.Opacity), fade);
            });
            await Task.Delay(150, cancellationToken);
        }
        finally
        {
            await RunOnDispatcherAsync(dispatcherQueue, () =>
            {
                root.Loaded -= HandleLoaded;
                washVisual.StopAnimation(nameof(Visual.Scale));
                rootVisual.StopAnimation(nameof(Visual.Opacity));
                PlatformWindowExtensions.viSetOpacity(handle, 0);

                if (shown)
                {
                    _ = ShowWindow(handle, ShowWindowHidden);
                }
            });
        }

        void HandleLoaded(object sender, RoutedEventArgs args) =>
            loaded.TrySetResult(true);
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
        int frames = 0;
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
            frames++;

            if (frames >= count)
            {
                completion.TrySetResult(true);
            }
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

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint window,
        int command);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;

        public int Y;
    }
}
