using Elysium.Platform.Windows;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System.Numerics;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;
using WinUIEx;
using PlatformWindowExtensions = Elysium.Platform.Windows.WindowExtensions;

namespace Glance.ScreenRecorder.WinUI;

internal sealed class RecordingBoundaryWindow :
    IDisposable
{
    private const int ExtendedWindowStyleIndex = -20;
    private const int StopHotKeyIdentifier = 0x474C;
    private const int ControlSurfaceWidth = 144;
    private const int ControlSurfaceHeight = 56;
    private const int ControlSurfaceGap = 12;
    private const int NoActivateExtendedWindowStyle = 0x08000000;
    private const int ToolWindowExtendedWindowStyle = 0x00000080;
    private const uint ExcludeFromCaptureAffinity = 0x00000011;
    private const uint HotKeyMessage = 0x0312;
    private const uint NonClientHitTestMessage = 0x0084;
    private const uint EscapeVirtualKey = 0x1B;
    private const int ClientHitTest = 1;
    private const int TransparentHitTest = -1;
    private const int RegionOr = 2;

    private readonly Border boundary;
    private readonly Border countdownSurface;
    private readonly TextBlock countdownText;
    private readonly DispatcherQueueTimer? trackingTimer;
    private readonly Button pauseButton;
    private readonly FontIcon pauseIcon;
    private readonly Button pointerButton;
    private readonly Border pointerSlash;
    private readonly Grid root;
    private readonly NativeRectangle desktopBounds;
    private readonly RecordingSource source;
    private readonly Border controlSurface;
    private readonly WindowSubclassProcedure windowProcedure;
    private readonly Window window;
    private readonly nint windowHandle;
    private NativeRectangle controlHitBounds;
    private readonly ITextLocalizer localizer;
    private bool disposed;

    public RecordingBoundaryWindow(RecordingSource source,
        NativeRectangle desktopBounds,
        DispatcherQueue dispatcherQueue,
        ITextLocalizer localizer)
    {
        this.source = source;
        this.desktopBounds = desktopBounds;
        this.localizer = localizer;
        CurrentBounds = source.Bounds;

        boundary = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 91, 103)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            IsHitTestVisible = false
        };
        countdownText = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 44,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Colors.White)
        };
        countdownSurface = new Border
        {
            Width = 84,
            Height = 84,
            Background = OverlayChrome.CreateAcrylicBrush(),
            CornerRadius = new CornerRadius(8),
            Child = countdownText,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        OverlayChrome.Elevate(countdownSurface, 48);
        root = new Grid
        {
            Background = new SolidColorBrush(Colors.Transparent)
        };
        root.Children.Add(boundary);
        root.Children.Add(countdownSurface);

        pauseIcon = new FontIcon
        {
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 14,
            Glyph = "\uF8AE"
        };
        pauseButton = CreateToolbarButton(pauseIcon, useAccentStyle: false);
        pauseButton.IsEnabled = false;
        SetButtonLabel(pauseButton, localizer.GetText("PauseRecording"));
        pauseButton.Click += HandlePauseClick;

        Grid pointerContent = new()
        {
            Width = 20,
            Height = 20
        };
        pointerContent.Children.Add(new FontIcon
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 16,
            Glyph = "\uE962"
        });
        pointerSlash = new Border
        {
            Width = 18,
            Height = 1.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = ResolveTextBrush(),
            RenderTransform = new RotateTransform { Angle = -45 },
            Visibility = Visibility.Collapsed
        };
        pointerContent.Children.Add(pointerSlash);
        pointerButton = CreateToolbarButton(pointerContent, useAccentStyle: false);
        pointerButton.IsEnabled = false;
        SetButtonLabel(pointerButton, localizer.GetText("HidePointerFromRecording"));
        pointerButton.Click += HandlePointerClick;

        FontIcon stopIcon = new()
        {
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 14,
            Glyph = "\uE7C8"
        };
        Button stopButton = CreateToolbarButton(stopIcon, useAccentStyle: true);
        string stopLabel = localizer.GetText("StopRecording");
        SetButtonLabel(stopButton, stopLabel);
        stopButton.Click += HandleStopClick;

        StackPanel controls = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4
        };
        controls.Children.Add(pauseButton);
        controls.Children.Add(pointerButton);
        controls.Children.Add(stopButton);
        controlSurface = new Border
        {
            Width = ControlSurfaceWidth,
            Height = ControlSurfaceHeight,
            Padding = new Thickness(8),
            Background = OverlayChrome.CreateAcrylicBrush(),
            CornerRadius = new CornerRadius(28),
            Child = controls
        };
        OverlayChrome.Elevate(controlSurface, 48);
        root.Children.Add(controlSurface);
        root.SizeChanged += HandleSizeChanged;

        window = new Window
        {
            Content = root,
            ExtendsContentIntoTitleBar = true,
            SystemBackdrop = new TransparentTintBackdrop()
        };
        window.SetTitleBar(null);
        windowHandle = WindowNative.GetWindowHandle(window);
        windowProcedure = HandleWindowMessage;

        if (source.Mode == ScreenRecordingMode.Window)
        {
            trackingTimer = dispatcherQueue.CreateTimer();
            trackingTimer.Interval = TimeSpan.FromMilliseconds(50);
            trackingTimer.IsRepeating = true;
            trackingTimer.Tick += HandleTrackingTick;
        }
    }

    public event EventHandler? StopRequested;

    public event EventHandler? PauseToggleRequested;

    public event EventHandler? CursorCaptureToggleRequested;

    public NativeRectangle CurrentBounds { get; private set; }

    public void Show()
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
        int extendedStyle = GetWindowLong(windowHandle, ExtendedWindowStyleIndex);
        _ = SetWindowLong(windowHandle,
            ExtendedWindowStyleIndex,
            extendedStyle | NoActivateExtendedWindowStyle | ToolWindowExtendedWindowStyle);
        _ = SetWindowDisplayAffinity(windowHandle, ExcludeFromCaptureAffinity);
        appWindow.IsShownInSwitchers = false;
        appWindow.MoveAndResize(new RectInt32(desktopBounds.Left, desktopBounds.Top, desktopBounds.Width, desktopBounds.Height));
        _ = SetWindowSubclass(windowHandle, windowProcedure, StopHotKeyIdentifier, 0);
        _ = RegisterHotKey(windowHandle, StopHotKeyIdentifier, 0, EscapeVirtualKey);
        appWindow.Show(false);
        UpdateBounds();
        trackingTimer?.Start();
    }

    public async Task ShowCountdownAsync(int value, CancellationToken cancellationToken)
    {
        if (value <= 0)
        {
            return;
        }

        countdownSurface.Visibility = Visibility.Visible;
        UpdateBounds();
        cancellationToken.ThrowIfCancellationRequested();
        countdownText.Text = value.ToString();
        PlayCountdownBeat();
        await Task.Delay(1000, cancellationToken);
    }

    public void SetRecording()
    {
        countdownSurface.Visibility = Visibility.Collapsed;
        boundary.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 65, 78));
        pauseButton.IsEnabled = true;
        pointerButton.IsEnabled = true;
        UpdateBounds();
    }

    public void SetPaused(bool paused)
    {
        pauseIcon.Glyph = paused ? "\uF5B0" : "\uF8AE";
        SetButtonLabel(pauseButton, localizer.GetText(paused ? "ResumeRecording" : "PauseRecording"));
        boundary.BorderBrush = new SolidColorBrush(paused
            ? Color.FromArgb(255, 255, 185, 70)
            : Color.FromArgb(255, 255, 65, 78));
    }

    public void SetCursorCaptureEnabled(bool enabled)
    {
        pointerSlash.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        SetButtonLabel(pointerButton, localizer.GetText(enabled
            ? "HidePointerFromRecording"
            : "ShowPointerInRecording"));
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        trackingTimer?.Stop();

        trackingTimer?.Tick -= HandleTrackingTick;

        root.SizeChanged -= HandleSizeChanged;
        pauseButton.Click -= HandlePauseClick;
        pointerButton.Click -= HandlePointerClick;
        _ = UnregisterHotKey(windowHandle, StopHotKeyIdentifier);
        _ = RemoveWindowSubclass(windowHandle, windowProcedure, StopHotKeyIdentifier);

        try
        {
            PlatformWindowExtensions.viSetOpacity(windowHandle, 0);
            window.AppWindow.Hide();
            window.Close();
            _ = DwmFlush();
        }
        catch (COMException)
        {
        }
    }

    private void HandleStopClick(object sender, RoutedEventArgs args) => StopRequested?.Invoke(this, EventArgs.Empty);

    private void HandlePauseClick(object sender, RoutedEventArgs args) => PauseToggleRequested?.Invoke(this, EventArgs.Empty);

    private void HandlePointerClick(object sender, RoutedEventArgs args) => CursorCaptureToggleRequested?.Invoke(this, EventArgs.Empty);

    private static Button CreateToolbarButton(UIElement content, bool useAccentStyle)
    {
        Button button = new()
        {
            Width = 40,
            Height = 40,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(20),
            Content = content
        };
        string styleKey = useAccentStyle ? "AccentButtonStyle" : "SubtleButtonStyle";

        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(styleKey, out object value) && value is Style style)
        {
            button.Style = style;
        }

        return button;
    }

    private static void SetButtonLabel(Button button, string label)
    {
        AutomationProperties.SetName(button, label);
        ToolTipService.SetToolTip(button, label);
    }

    private nint HandleWindowMessage(nint window,
        uint message,
        nuint wParam,
        nint lParam,
        nuint subclassIdentifier,
        nuint referenceData)
    {
        if (message == HotKeyMessage && wParam == StopHotKeyIdentifier)
        {
            StopRequested?.Invoke(this, EventArgs.Empty);
            return 0;
        }

        if (message == NonClientHitTestMessage)
        {
            int pointerX = unchecked((short)((long)lParam & 0xFFFF));
            int pointerY = unchecked((short)(((long)lParam >> 16) & 0xFFFF));
            return pointerX >= controlHitBounds.Left && pointerX < controlHitBounds.Right &&
                pointerY >= controlHitBounds.Top && pointerY < controlHitBounds.Bottom
                    ? ClientHitTest
                    : TransparentHitTest;
        }

        return DefSubclassProc(window, message, wParam, lParam);
    }

    private void HandleTrackingTick(DispatcherQueueTimer sender, object args) => UpdateBounds();

    private void HandleSizeChanged(object sender, SizeChangedEventArgs args) => UpdateBounds();

    private void PlayCountdownBeat()
    {
        root.UpdateLayout();
        Visual visual = ElementCompositionPreview.GetElementVisual(countdownText);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1));
        Vector3KeyFrameAnimation scale = compositor.CreateVector3KeyFrameAnimation();
        ScalarKeyFrameAnimation opacity = compositor.CreateScalarKeyFrameAnimation();

        visual.CenterPoint = new Vector3(visual.Size.X / 2, visual.Size.Y / 2, 0);
        scale.InsertKeyFrame(0, new Vector3(0.74f, 0.74f, 1));
        scale.InsertKeyFrame(1, Vector3.One, easing);
        scale.Duration = TimeSpan.FromMilliseconds(240);
        opacity.InsertKeyFrame(0, 0);
        opacity.InsertKeyFrame(0.28f, 1, easing);
        opacity.InsertKeyFrame(0.78f, 1);
        opacity.InsertKeyFrame(1, 0);
        opacity.Duration = TimeSpan.FromMilliseconds(900);
        visual.StartAnimation(nameof(Visual.Scale), scale);
        visual.StartAnimation(nameof(Visual.Opacity), opacity);
    }

    private void UpdateBounds()
    {
        NativeRectangle bounds = source.Bounds;

        if (source.WindowHandle != nint.Zero && TryGetWindowBounds(source.WindowHandle, out NativeRectangle currentBounds))
        {
            bounds = currentBounds;
        }

        CurrentBounds = bounds;

        double scaleX = root.ActualWidth > 0 ? root.ActualWidth / desktopBounds.Width : 1;
        double scaleY = root.ActualHeight > 0 ? root.ActualHeight / desktopBounds.Height : 1;
        boundary.Margin = new Thickness((bounds.Left - desktopBounds.Left) * scaleX,
            (bounds.Top - desktopBounds.Top) * scaleY,
            Math.Max(0, (desktopBounds.Right - bounds.Right) * scaleX),
            Math.Max(0, (desktopBounds.Bottom - bounds.Bottom) * scaleY));
        boundary.HorizontalAlignment = HorizontalAlignment.Stretch;
        boundary.VerticalAlignment = VerticalAlignment.Stretch;

        double centerX = (((bounds.Left + bounds.Right) / 2d) - desktopBounds.Left) * scaleX;
        double centerY = (((bounds.Top + bounds.Bottom) / 2d) - desktopBounds.Top) * scaleY;
        countdownSurface.HorizontalAlignment = HorizontalAlignment.Left;
        countdownSurface.VerticalAlignment = VerticalAlignment.Top;
        countdownSurface.Margin = new Thickness(centerX - 42, centerY - 42, 0, 0);

        int controlPhysicalWidth = Math.Max(1, (int)Math.Round(ControlSurfaceWidth / scaleX));
        int controlPhysicalHeight = Math.Max(1, (int)Math.Round(ControlSurfaceHeight / scaleY));
        int controlPhysicalGap = Math.Max(1, (int)Math.Round(ControlSurfaceGap / scaleY));
        int controlLeft = bounds.Left + ((bounds.Width - controlPhysicalWidth) / 2);
        int controlTop = bounds.Top - controlPhysicalHeight - controlPhysicalGap;

        if (controlTop < desktopBounds.Top)
        {
            controlTop = bounds.Top + controlPhysicalGap;
        }

        controlLeft = Math.Clamp(controlLeft, desktopBounds.Left, desktopBounds.Right - controlPhysicalWidth);
        controlTop = Math.Clamp(controlTop, desktopBounds.Top, desktopBounds.Bottom - controlPhysicalHeight);
        controlHitBounds = new NativeRectangle(controlLeft,
            controlTop,
            controlLeft + controlPhysicalWidth,
            controlTop + controlPhysicalHeight);
        controlSurface.HorizontalAlignment = HorizontalAlignment.Left;
        controlSurface.VerticalAlignment = VerticalAlignment.Top;
        controlSurface.Margin = new Thickness((controlLeft - desktopBounds.Left) * scaleX,
            (controlTop - desktopBounds.Top) * scaleY,
            0,
            0);
        ApplyWindowRegion(bounds, scaleX, scaleY);
    }

    private void ApplyWindowRegion(NativeRectangle bounds, double scaleX, double scaleY)
    {
        int left = Math.Clamp(bounds.Left - desktopBounds.Left, 0, desktopBounds.Width);
        int top = Math.Clamp(bounds.Top - desktopBounds.Top, 0, desktopBounds.Height);
        int right = Math.Clamp(bounds.Right - desktopBounds.Left, 0, desktopBounds.Width);
        int bottom = Math.Clamp(bounds.Bottom - desktopBounds.Top, 0, desktopBounds.Height);
        int borderWidth = Math.Max(2, (int)Math.Ceiling(3 / Math.Max(scaleX, 0.01)));
        int borderHeight = Math.Max(2, (int)Math.Ceiling(3 / Math.Max(scaleY, 0.01)));
        nint windowRegion = CreateRectRgn(0, 0, 0, 0);

        AddRectangleToRegion(windowRegion, left, top, right, Math.Min(bottom, top + borderHeight));
        AddRectangleToRegion(windowRegion, left, Math.Max(top, bottom - borderHeight), right, bottom);
        AddRectangleToRegion(windowRegion, left, top, Math.Min(right, left + borderWidth), bottom);
        AddRectangleToRegion(windowRegion, Math.Max(left, right - borderWidth), top, right, bottom);

        int controlLeft = controlHitBounds.Left - desktopBounds.Left;
        int controlTop = controlHitBounds.Top - desktopBounds.Top;
        int controlRight = controlHitBounds.Right - desktopBounds.Left;
        int controlBottom = controlHitBounds.Bottom - desktopBounds.Top;
        nint controlRegion = CreateRoundRectRgn(controlLeft,
            controlTop,
            controlRight + 1,
            controlBottom + 1,
            controlBottom - controlTop,
            controlBottom - controlTop);
        _ = CombineRgn(windowRegion, windowRegion, controlRegion, RegionOr);
        _ = DeleteObject(controlRegion);

        if (countdownSurface.Visibility == Visibility.Visible)
        {
            int countdownWidth = Math.Max(1, (int)Math.Round(84 / Math.Max(scaleX, 0.01)));
            int countdownHeight = Math.Max(1, (int)Math.Round(84 / Math.Max(scaleY, 0.01)));
            int countdownLeft = left + ((right - left - countdownWidth) / 2);
            int countdownTop = top + ((bottom - top - countdownHeight) / 2);
            AddRectangleToRegion(windowRegion,
                countdownLeft,
                countdownTop,
                countdownLeft + countdownWidth,
                countdownTop + countdownHeight);
        }

        if (SetWindowRgn(windowHandle, windowRegion, true) == 0)
        {
            _ = DeleteObject(windowRegion);
        }
    }

    private static void AddRectangleToRegion(nint targetRegion, int left, int top, int right, int bottom)
    {
        if (right <= left || bottom <= top)
        {
            return;
        }

        nint rectangle = CreateRectRgn(left, top, right, bottom);
        _ = CombineRgn(targetRegion, targetRegion, rectangle, RegionOr);
        _ = DeleteObject(rectangle);
    }

    private static bool TryGetWindowBounds(nint window, out NativeRectangle bounds)
    {
        if (DwmGetWindowAttribute(window, 9, out NativeRect rectangle, Marshal.SizeOf<NativeRect>()) == 0 &&
            rectangle.Right > rectangle.Left && rectangle.Bottom > rectangle.Top)
        {
            bounds = new NativeRectangle(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);
            return true;
        }

        bounds = default;
        return false;
    }

    private static Brush ResolveAcrylicBrush() => Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("AcrylicInAppFillColorDefaultBrush", out object value) && value is Brush brush
            ? brush
            : new SolidColorBrush(Color.FromArgb(235, 30, 30, 30));

    private static Brush ResolveTextBrush() => Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("TextFillColorPrimaryBrush", out object value) && value is Brush brush
            ? brush
            : new SolidColorBrush(Colors.White);

    private delegate nint WindowSubclassProcedure(nint window,
        uint message,
        nuint wParam,
        nint lParam,
        nuint subclassIdentifier,
        nuint referenceData);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint window, int attribute, out NativeRect value, int valueSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint window, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(nint window, int index, int newValue);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(nint window, uint affinity);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(nint window, nint region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [DllImport("gdi32.dll")]
    private static extern nint CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("gdi32.dll")]
    private static extern nint CreateRoundRectRgn(int left, int top, int right, int bottom, int ellipseWidth, int ellipseHeight);

    [DllImport("gdi32.dll")]
    private static extern int CombineRgn(nint destination, nint source1, nint source2, int mode);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint value);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint window, int identifier, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint window, int identifier);

    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint window,
        WindowSubclassProcedure procedure,
        nuint subclassIdentifier,
        nuint referenceData);

    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint window,
        WindowSubclassProcedure procedure,
        nuint subclassIdentifier);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint window, uint message, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
