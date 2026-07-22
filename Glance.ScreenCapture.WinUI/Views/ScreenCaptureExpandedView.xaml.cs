using Elysium.UI.Controls.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Runtime.InteropServices;
using Windows.Foundation;

namespace Glance.ScreenCapture.WinUI;

public sealed partial class ScreenCaptureExpandedView : UserControl
{
    private const int CaptureCompletionDelayMs = 700;

    private DispatcherQueueTimer? captureCompletionTimer;
    private bool isCaptureInProgress;

    public ScreenCaptureExpandedView(ScreenCaptureViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public ScreenCaptureViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    internal void SetCaptureInProgress(bool value)
    {
        captureCompletionTimer?.Stop();
        isCaptureInProgress = value;
        DesktopIsland? island = FindIsland();

        if (island is null)
        {
            return;
        }

        island.IsExpansionLocked = value;

        if (value)
        {
            island.Reveal();
            island.IsExpanded = true;
        }
    }

    internal void CompleteCapturePresentation()
    {
        captureCompletionTimer ??= CreateCaptureCompletionTimer();
        captureCompletionTimer.Stop();
        captureCompletionTimer.Start();
    }

    private DispatcherQueueTimer CreateCaptureCompletionTimer()
    {
        DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(CaptureCompletionDelayMs);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => SetCaptureInProgress(false);
        return timer;
    }

    internal bool TryGetCaptureLandingBounds(out NativeRectangle bounds)
    {
        bounds = default;
        DesktopIsland? island = FindIsland();

        if (island is null || !CaptureLandingTarget.IsLoaded || CaptureLandingTarget.ActualWidth <= 0 || CaptureLandingTarget.ActualHeight <= 0 || !GetWindowRect(island.Handle, out NativeRect windowBounds))
        {
            return false;
        }

        Rect localBounds = CaptureLandingTarget.TransformToVisual(island).TransformBounds(new Rect(0, 0, CaptureLandingTarget.ActualWidth, CaptureLandingTarget.ActualHeight));
        double scale = XamlRoot?.RasterizationScale ?? 1;
        bounds = new NativeRectangle(
            windowBounds.Left + (int)Math.Round(localBounds.X * scale),
            windowBounds.Top + (int)Math.Round(localBounds.Y * scale),
            Math.Max(1, (int)Math.Round(localBounds.Width * scale)),
            Math.Max(1, (int)Math.Round(localBounds.Height * scale)));
        return true;
    }

    private void HandleCaptureMenuOpened(object sender, object args) =>
        SetExpansionLocked(true);

    private void HandleCaptureMenuClosed(object sender, object args)
    {
        if (!isCaptureInProgress)
        {
            SetExpansionLocked(false);
        }
    }

    private void SetExpansionLocked(bool isLocked)
    {
        DependencyObject? current = this;

        while (current is not null)
        {
            if (current is DesktopIsland island)
            {
                island.IsExpansionLocked = isLocked;
                return;
            }

            current = VisualTreeHelper.GetParent(current);
        }
    }

    private DesktopIsland? FindIsland()
    {
        DependencyObject? current = this;

        while (current is not null)
        {
            if (current is DesktopIsland island)
            {
                return island;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private string ToUpper(string value) => value.ToUpperInvariant();

    private bool WhenIdle(bool isCapturing) => !isCapturing;

    private Visibility WhenEmpty(bool hasCaptures) =>
        hasCaptures ? Visibility.Collapsed : Visibility.Visible;

    private Visibility WhenPopulated(bool hasCaptures) =>
        hasCaptures ? Visibility.Visible : Visibility.Collapsed;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect bounds);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
