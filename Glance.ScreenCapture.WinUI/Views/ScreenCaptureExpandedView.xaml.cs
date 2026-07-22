using Elysium.UI.Controls.WinUI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Windows.Foundation;

namespace Glance.ScreenCapture.WinUI;

public sealed partial class ScreenCaptureExpandedView : UserControl
{
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

    internal void PrepareCaptureArrival()
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(CaptureArrivalContent);
        visual.CenterPoint = new Vector3((float)CaptureArrivalContent.ActualWidth / 2, (float)CaptureArrivalContent.ActualHeight / 2, 0);
        visual.Opacity = 0;
        visual.Scale = new Vector3(0.94f, 0.94f, 1);
    }

    internal void PlayCaptureArrival()
    {
        Visual contentVisual = ElementCompositionPreview.GetElementVisual(CaptureArrivalContent);
        Visual glowVisual = ElementCompositionPreview.GetElementVisual(CaptureArrivalGlow);
        Compositor compositor = contentVisual.Compositor;
        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1), new Vector2(0.3f, 1));

        Vector3KeyFrameAnimation scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.Duration = TimeSpan.FromMilliseconds(240);
        scaleAnimation.InsertKeyFrame(0, new Vector3(0.94f, 0.94f, 1));
        scaleAnimation.InsertKeyFrame(1, Vector3.One, easing);
        ScalarKeyFrameAnimation opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.Duration = TimeSpan.FromMilliseconds(180);
        opacityAnimation.InsertKeyFrame(0, 0);
        opacityAnimation.InsertKeyFrame(1, 1, easing);

        glowVisual.CenterPoint = new Vector3((float)CaptureArrivalGlow.ActualWidth / 2, (float)CaptureArrivalGlow.ActualHeight / 2, 0);
        Vector3KeyFrameAnimation glowScaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        glowScaleAnimation.Duration = TimeSpan.FromMilliseconds(320);
        glowScaleAnimation.InsertKeyFrame(0, new Vector3(0.82f, 0.82f, 1));
        glowScaleAnimation.InsertKeyFrame(1, new Vector3(1.06f, 1.06f, 1), easing);
        ScalarKeyFrameAnimation glowOpacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        glowOpacityAnimation.Duration = TimeSpan.FromMilliseconds(320);
        glowOpacityAnimation.InsertKeyFrame(0, 0.38f);
        glowOpacityAnimation.InsertKeyFrame(1, 0, easing);

        contentVisual.StartAnimation(nameof(Visual.Scale), scaleAnimation);
        contentVisual.StartAnimation(nameof(Visual.Opacity), opacityAnimation);
        glowVisual.StartAnimation(nameof(Visual.Scale), glowScaleAnimation);
        glowVisual.StartAnimation(nameof(Visual.Opacity), glowOpacityAnimation);
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
