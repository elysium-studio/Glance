using Elysium.Platform.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Storage.Streams;
using WinRT.Interop;
using WinUIEx;
using PlatformWindowExtensions = Elysium.Platform.Windows.WindowExtensions;

namespace Glance.ThemeSwitcher.WinUI;

public sealed partial class ThemeTransitionService(ILogger<ThemeTransitionService> logger) :
    IDisposable
{
    private const uint CaptureBlt = 0x40000000;
    private const int ExtendedWindowStyleIndex = -20;
    private const int NoActivateWindowStyle = 0x08000000;
    private const int ShowWindowHidden = 0;
    private const uint SourceCopy = 0x00CC0020;
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
        InMemoryRandomAccessStream snapshotStream;
        LoadedImageSurface snapshotSurface;

        try
        {
            byte[] snapshot = CaptureDisplay(bounds);
            snapshotStream = CreateBitmapStream(bounds.Width, bounds.Height, snapshot);

            try
            {
                snapshotSurface = await LoadSurfaceAsync(snapshotStream, cancellationToken);
            }
            catch
            {
                snapshotStream.Dispose();
                throw;
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Unable to prepare the captured display for the {Theme} theme transition", theme);
            await applyTheme();
            return;
        }

        Canvas root = new()
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            IsHitTestVisible = false
        };

        Window transitionWindow = GetWindow(root, bounds);
        Visual rootVisual = ElementCompositionPreview.GetElementVisual(root);
        rootVisual.Opacity = 1;
        Compositor compositor = rootVisual.Compositor;
        CompositionSurfaceBrush snapshotBrush = compositor.CreateSurfaceBrush(snapshotSurface);
        snapshotBrush.Stretch = CompositionStretch.Fill;
        CompositionRadialGradientBrush cutoutBrush = compositor.CreateRadialGradientBrush();
        cutoutBrush.MappingMode = CompositionMappingMode.Relative;
        cutoutBrush.CenterPoint = new Vector2((float)(localX / bounds.Width), (float)(localY / bounds.Height));
        cutoutBrush.EllipseRadius = new Vector2(1f / bounds.Width, 1f / bounds.Height);
        cutoutBrush.ColorStops.Insert(0, compositor.CreateColorGradientStop(0, Windows.UI.Color.FromArgb(0, 255, 255, 255)));
        cutoutBrush.ColorStops.Insert(1, compositor.CreateColorGradientStop(0.985f, Windows.UI.Color.FromArgb(0, 255, 255, 255)));
        cutoutBrush.ColorStops.Insert(2, compositor.CreateColorGradientStop(1, Windows.UI.Color.FromArgb(255, 255, 255, 255)));
        SpriteVisual maskVisual = compositor.CreateSpriteVisual();
        maskVisual.Brush = cutoutBrush;
        maskVisual.Size = new Vector2(bounds.Width, bounds.Height);
        CompositionVisualSurface maskSurface = compositor.CreateVisualSurface();
        maskSurface.SourceVisual = maskVisual;
        maskSurface.SourceSize = maskVisual.Size;
        CompositionSurfaceBrush maskSurfaceBrush = compositor.CreateSurfaceBrush(maskSurface);
        maskSurfaceBrush.Stretch = CompositionStretch.Fill;
        CompositionMaskBrush maskedSnapshotBrush = compositor.CreateMaskBrush();
        maskedSnapshotBrush.Source = snapshotBrush;
        maskedSnapshotBrush.Mask = maskSurfaceBrush;
        SpriteVisual snapshotVisual = compositor.CreateSpriteVisual();
        snapshotVisual.Brush = maskedSnapshotBrush;
        snapshotVisual.RelativeSizeAdjustment = Vector2.One;
        ElementCompositionPreview.SetElementChildVisual(root, snapshotVisual);
        DispatcherQueue dispatcherQueue = transitionWindow.DispatcherQueue;
        bool shown = false;

        try
        {
            shown = true;
            await ShowPreparedAsync(transitionWindow, root, bounds, handle, cancellationToken);
            await applyTheme().ConfigureAwait(false);
            await WaitForRenderingFramesAsync(dispatcherQueue, 3, cancellationToken);
            await RunOnDispatcherAsync(dispatcherQueue, () =>
            {
                float radius = (float)GetCoveringRadius(localX, localY, bounds.Width, bounds.Height) * 1.02f;
                Vector2 initialRadius = new(1f / bounds.Width, 1f / bounds.Height);
                Vector2 finalRadius = new(radius / bounds.Width, radius / bounds.Height);
                CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1), new Vector2(0.3f, 1));
                Vector2KeyFrameAnimation reveal = compositor.CreateVector2KeyFrameAnimation();
                reveal.InsertKeyFrame(0, initialRadius, easing);
                reveal.InsertKeyFrame(1, finalRadius, easing);
                reveal.Duration = TimeSpan.FromMilliseconds(300);
                cutoutBrush.EllipseRadius = finalRadius;
                cutoutBrush.StartAnimation(nameof(CompositionRadialGradientBrush.EllipseRadius), reveal);
            });
            await Task.Delay(300, cancellationToken);
        }
        finally
        {
            await RunOnDispatcherAsync(dispatcherQueue, () =>
            {
                cutoutBrush.StopAnimation(nameof(CompositionRadialGradientBrush.EllipseRadius));
                ElementCompositionPreview.SetElementChildVisual(root, null);
                PlatformWindowExtensions.viSetOpacity(handle, 0);

                if (shown)
                {
                    _ = ShowWindow(handle, ShowWindowHidden);
                }
            });
            snapshotVisual.Dispose();
            maskedSnapshotBrush.Dispose();
            maskSurfaceBrush.Dispose();
            maskSurface.Dispose();
            maskVisual.Dispose();
            cutoutBrush.Dispose();
            snapshotBrush.Dispose();
            snapshotSurface.Dispose();
            snapshotStream.Dispose();
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

    private static double GetCoveringRadius(double x,
        double y,
        double width,
        double height)
    {
        double horizontal = Math.Max(x, width - x);
        double vertical = Math.Max(y, height - y);
        return Math.Sqrt(horizontal * horizontal + vertical * vertical);
    }

    private static byte[] CaptureDisplay(RectInt32 bounds)
    {
        _ = DwmFlush();
        nint screenDeviceContext = GetDC(nint.Zero);

        if (screenDeviceContext == nint.Zero)
        {
            throw new InvalidOperationException("Unable to access the desktop surface.");
        }

        nint memoryDeviceContext = CreateCompatibleDC(screenDeviceContext);
        nint bitmap = CreateCompatibleBitmap(screenDeviceContext, bounds.Width, bounds.Height);
        nint previousBitmap = SelectObject(memoryDeviceContext, bitmap);

        try
        {
            if (!BitBlt(memoryDeviceContext, 0, 0, bounds.Width, bounds.Height, screenDeviceContext, bounds.X, bounds.Y, SourceCopy | CaptureBlt))
            {
                throw new InvalidOperationException("Unable to capture the current display.");
            }

            BitmapInfo bitmapInfo = new()
            {
                Header = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    Width = bounds.Width,
                    Height = -bounds.Height,
                    Planes = 1,
                    BitCount = 32
                }
            };
            byte[] pixels = new byte[bounds.Width * bounds.Height * 4];

            if (GetDIBits(memoryDeviceContext, bitmap, 0, (uint)bounds.Height, pixels, ref bitmapInfo, 0) == 0)
            {
                throw new InvalidOperationException("Unable to read the captured display.");
            }

            for (int index = 3; index < pixels.Length; index += 4)
            {
                pixels[index] = byte.MaxValue;
            }

            return pixels;
        }
        finally
        {
            _ = SelectObject(memoryDeviceContext, previousBitmap);
            _ = DeleteObject(bitmap);
            _ = DeleteDC(memoryDeviceContext);
            _ = ReleaseDC(nint.Zero, screenDeviceContext);
        }
    }

    private static InMemoryRandomAccessStream CreateBitmapStream(int width,
        int height,
        byte[] pixels)
    {
        const int fileHeaderSize = 14;
        const int informationHeaderSize = 40;
        int pixelOffset = fileHeaderSize + informationHeaderSize;
        InMemoryRandomAccessStream randomAccessStream = new();
        Stream stream = randomAccessStream.AsStream();

        using (BinaryWriter writer = new(stream, Encoding.UTF8, true))
        {
            writer.Write((ushort)0x4D42);
            writer.Write(pixelOffset + pixels.Length);
            writer.Write(0);
            writer.Write(pixelOffset);
            writer.Write(informationHeaderSize);
            writer.Write(width);
            writer.Write(-height);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(0);
            writer.Write(pixels.Length);
            writer.Write((int)(96 * 39.3701));
            writer.Write((int)(96 * 39.3701));
            writer.Write(0);
            writer.Write(0);
            writer.Write(pixels);
            writer.Flush();
        }

        randomAccessStream.Seek(0);
        return randomAccessStream;
    }

    private static async Task<LoadedImageSurface> LoadSurfaceAsync(InMemoryRandomAccessStream stream,
        CancellationToken cancellationToken)
    {
        LoadedImageSurface surface = LoadedImageSurface.StartLoadFromStream(stream);
        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        surface.LoadCompleted += HandleLoadCompleted;

        try
        {
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            return surface;
        }
        catch
        {
            surface.Dispose();
            throw;
        }
        finally
        {
            surface.LoadCompleted -= HandleLoadCompleted;
        }

        void HandleLoadCompleted(LoadedImageSurface sender,
            LoadedImageSourceLoadCompletedEventArgs args)
        {
            if (args.Status == LoadedImageSourceLoadStatus.Success)
            {
                completion.TrySetResult(true);
            }
            else
            {
                completion.TrySetException(new InvalidOperationException($"Unable to load the display snapshot: {args.Status}."));
            }
        }
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

        await RunOnDispatcherAsync(dispatcherQueue, () =>
        {
            root.Loaded += HandleLoaded;
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

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool BitBlt(nint destination,
        int destinationX,
        int destinationY,
        int width,
        int height,
        nint source,
        int sourceX,
        int sourceY,
        uint operation);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleBitmap(nint deviceContext,
        int width,
        int height);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleDC(nint deviceContext);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteDC(nint deviceContext);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint value);

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint window);

    [LibraryImport("gdi32.dll")]
    private static partial int GetDIBits(nint deviceContext,
        nint bitmap,
        uint start,
        uint lines,
        byte[] pixels,
        ref BitmapInfo bitmapInfo,
        uint usage);

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
    private static partial int ReleaseDC(nint window,
        nint deviceContext);

    [LibraryImport("gdi32.dll")]
    private static partial nint SelectObject(nint deviceContext,
        nint value);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint window,
        int command);

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;

        public uint Colors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;

        public int Width;

        public int Height;

        public ushort Planes;

        public ushort BitCount;

        public uint Compression;

        public uint ImageSize;

        public int XPixelsPerMeter;

        public int YPixelsPerMeter;

        public uint ColorsUsed;

        public uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;

        public int Y;
    }
}
