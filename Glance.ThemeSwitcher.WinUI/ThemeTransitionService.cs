using Microsoft.UI.Composition;
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
using WinRT;
using WinRT.Interop;
using WinUIEx;

namespace Glance.ThemeSwitcher.WinUI;

public sealed partial class ThemeTransitionService
{
    private const int CaptionStyle = 0x00C00000;
    private const uint DwmBorderColorAttribute = 34;
    private const uint DwmColorNone = 0xFFFFFFFE;
    private const uint DwmCornerPreferenceAttribute = 33;
    private const uint DwmDoNotRound = 1;
    private const int ExtendedWindowStyleIndex = -20;
    private const int NoActivateWindowStyle = 0x08000000;
    private const int ResizableFrameStyle = 0x00040000;
    private const int SystemMenuStyle = 0x00080000;
    private const int ToolWindowStyle = 0x00000080;
    private const int TransparentWindowStyle = 0x00000020;
    private const int WindowStyleIndex = -16;

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

        Window window = CreateWindow(root, bounds);
        Visual washVisual = ElementCompositionPreview.GetElementVisual(wash);
        washVisual.CenterPoint = new Vector3((float)radius, (float)radius, 0);
        washVisual.Scale = Vector3.Zero;
        Visual rootVisual = ElementCompositionPreview.GetElementVisual(root);
        rootVisual.Opacity = 1;

        TaskCompletionSource<bool> loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        root.Loaded += (_, _) => loaded.TrySetResult(true);
        window.AppWindow.Show(activateWindow: false);

        try
        {
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            Compositor compositor = washVisual.Compositor;
            CubicBezierEasingFunction revealEasing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1), new Vector2(0.3f, 1));
            Vector3KeyFrameAnimation reveal = compositor.CreateVector3KeyFrameAnimation();
            reveal.InsertKeyFrame(0, new Vector3(0.015f), revealEasing);
            reveal.InsertKeyFrame(1, Vector3.One, revealEasing);
            reveal.Duration = TimeSpan.FromMilliseconds(300);
            washVisual.StartAnimation(nameof(Visual.Scale), reveal);

            await Task.Delay(145, cancellationToken);
            await applyTheme();
            await Task.Delay(155, cancellationToken);

            ScalarKeyFrameAnimation fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0, 1);
            fade.InsertKeyFrame(1, 0, compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0), new Vector2(1, 1)));
            fade.Duration = TimeSpan.FromMilliseconds(150);
            rootVisual.StartAnimation(nameof(Visual.Opacity), fade);
            await Task.Delay(150, cancellationToken);
        }
        finally
        {
            window.Close();
        }
    }

    private static Window CreateWindow(UIElement content,
        RectInt32 bounds)
    {
        Window window = new()
        {
            Content = content,
            ExtendsContentIntoTitleBar = true,
            SystemBackdrop = new TransparentTintBackdrop()
        };
        window.SetTitleBar(null);
        window.AppWindow.IsShownInSwitchers = false;

        OverlappedPresenter presenter = window.AppWindow.Presenter.As<OverlappedPresenter>();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsAlwaysOnTop = true;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsResizable = false;

        nint handle = WindowNative.GetWindowHandle(window);
        int style = GetWindowLong(handle, WindowStyleIndex);
        _ = SetWindowLong(handle, WindowStyleIndex, style & ~CaptionStyle & ~ResizableFrameStyle & ~SystemMenuStyle);
        int extendedStyle = GetWindowLong(handle, ExtendedWindowStyleIndex);
        _ = SetWindowLong(handle, ExtendedWindowStyleIndex, extendedStyle | TransparentWindowStyle | ToolWindowStyle | NoActivateWindowStyle);
        uint cornerPreference = DwmDoNotRound;
        uint borderColor = DwmColorNone;
        _ = DwmSetWindowAttribute(handle, DwmCornerPreferenceAttribute, in cornerPreference, sizeof(uint));
        _ = DwmSetWindowAttribute(handle, DwmBorderColorAttribute, in borderColor, sizeof(uint));
        window.AppWindow.MoveAndResize(bounds);
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

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint window,
        uint attribute,
        in uint value,
        uint size);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;

        public int Y;
    }
}
