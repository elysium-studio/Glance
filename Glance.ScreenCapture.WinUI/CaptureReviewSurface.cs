using Glance.Application.Abstractions;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Glance.ScreenCapture.WinUI;

internal sealed class CaptureReviewSurface
{
    private const int DismissDurationMs = 240;
    private const int EntranceDurationMs = 280;
    private readonly Border animationPreview;
    private readonly double availableHeight;
    private readonly DesktopCaptureBitmap bitmap;
    private readonly TaskCompletionSource<DesktopCaptureBitmap?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Border previewHost;
    private readonly Border reviewBackdrop;
    private readonly Grid reviewLayer;
    private readonly Border toolbar;
    private DispatcherQueueTimer? dismissTimer;
    private DispatcherQueueTimer? entranceTimer;
    private bool completed;
    private bool transitioning;

    public CaptureReviewSurface(DesktopCaptureBitmap bitmap, ITextLocalizer localizer, double availableWidth, double availableHeight)
    {
        this.bitmap = bitmap;
        this.availableHeight = availableHeight;

        double maximumWidth = Math.Max(240, availableWidth - 96);
        double maximumHeight = Math.Max(160, availableHeight - 180);
        double scale = Math.Min(1, Math.Min(maximumWidth / bitmap.Width, maximumHeight / bitmap.Height));
        double previewWidth = Math.Max(1, Math.Round(bitmap.Width * scale));
        double previewHeight = Math.Max(1, Math.Round(bitmap.Height * scale));
        double previewX = Math.Round((availableWidth - previewWidth) / 2);
        double previewY = Math.Round((availableHeight - previewHeight + 42) / 2);
        PreviewBounds = new Rect(previewX, previewY, previewWidth, previewHeight);

        WriteableBitmap imageSource = CreateImageSource(bitmap);

        previewHost = CreatePreviewHost(imageSource, previewWidth, previewHeight);
        previewHost.Translation = new Vector3(0, 0, 32);

        Canvas.SetLeft(previewHost, previewX);
        Canvas.SetTop(previewHost, previewY);

        animationPreview = CreatePreviewHost(imageSource, previewWidth, previewHeight);
        animationPreview.Translation = new Vector3(0, 0, 40);

        Canvas.SetLeft(animationPreview, previewX);
        Canvas.SetTop(animationPreview, previewY);

        Button dismissButton = CreateToolbarButton("\uE711", localizer.GetText("DismissCapture"), false);
        dismissButton.Click += (_, _) => Dismiss();

        Button confirmButton = CreateToolbarButton("\uE73E", localizer.GetText("ConfirmCapture"), true);
        confirmButton.Click += (_, _) => Confirm();

        StackPanel toolbarContent = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        toolbarContent.Children.Add(dismissButton);
        toolbarContent.Children.Add(confirmButton);

        toolbar = new Border
        {
            Padding = new Thickness(6),
            Background = ResolveMicaBrush(),
            BorderBrush = ResolveBrush("SurfaceStrokeColorDefaultBrush", Windows.UI.Color.FromArgb(48, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            Child = toolbarContent,
            Shadow = new ThemeShadow()
        };

        toolbar.Translation = new Vector3(0, 0, 48);
        toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        double toolbarWidth = toolbar.DesiredSize.Width;
        double toolbarHeight = toolbar.DesiredSize.Height;

        Canvas.SetLeft(toolbar, Math.Round((availableWidth - toolbarWidth) / 2));
        Canvas.SetTop(toolbar, Math.Max(20, previewY - toolbarHeight - 14));

        reviewBackdrop = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(178, 8, 10, 14))
        };

        Canvas reviewCanvas = new()
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0))
        };

        reviewCanvas.Children.Add(previewHost);
        reviewCanvas.Children.Add(animationPreview);
        reviewCanvas.Children.Add(toolbar);

        reviewLayer = new Grid
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0)),
            IsTabStop = true
        };

        reviewLayer.Children.Add(reviewBackdrop);
        reviewLayer.Children.Add(reviewCanvas);
    }

    public FrameworkElement Content => reviewLayer;

    public Task<DesktopCaptureBitmap?> Completion => completion.Task;

    public Rect PreviewBounds { get; }

    public Rect SelectedPreviewBounds => PreviewBounds;

    public void Focus() => reviewLayer.Focus(FocusState.Programmatic);

    public void PlayEntrance(Rect sourceBounds)
    {
        Visual animationVisual = ElementCompositionPreview.GetElementVisual(animationPreview);
        Visual previewVisual = ElementCompositionPreview.GetElementVisual(previewHost);
        Visual toolbarVisual = ElementCompositionPreview.GetElementVisual(toolbar);
        Visual backdropVisual = ElementCompositionPreview.GetElementVisual(reviewBackdrop);
        Compositor compositor = animationVisual.Compositor;
        TimeSpan duration = TimeSpan.FromMilliseconds(EntranceDurationMs);
        SineEasingFunction easing = CompositionEasingFunction.CreateSineEasingFunction(compositor, CompositionEasingFunctionMode.Out);
        Vector3 animationOffset = animationVisual.Offset;
        Vector3 sourceCenter = new((float)(sourceBounds.X + (sourceBounds.Width / 2)), (float)(sourceBounds.Y + (sourceBounds.Height / 2)), 0);
        Vector3 targetCenter = new((float)(PreviewBounds.X + (PreviewBounds.Width / 2)), (float)(PreviewBounds.Y + (PreviewBounds.Height / 2)), 0);
        Vector3 sourceOffset = animationOffset + sourceCenter - targetCenter;
        Vector3 sourceScale = new((float)Math.Max(0.01, sourceBounds.Width / PreviewBounds.Width), (float)Math.Max(0.01, sourceBounds.Height / PreviewBounds.Height), 1);

        animationVisual.CenterPoint = new Vector3((float)PreviewBounds.Width / 2, (float)PreviewBounds.Height / 2, 0);
        animationVisual.Offset = animationOffset;
        animationVisual.Scale = Vector3.One;
        animationVisual.Opacity = 0;
        previewVisual.Opacity = 1;
        toolbarVisual.Opacity = 1;
        backdropVisual.Opacity = 1;

        animationVisual.StartAnimation(nameof(Visual.Offset), CreateVectorAnimation(compositor, sourceOffset, animationOffset, duration, easing));
        animationVisual.StartAnimation(nameof(Visual.Scale), CreateVectorAnimation(compositor, sourceScale, Vector3.One, duration, easing));
        animationVisual.StartAnimation(nameof(Visual.Opacity), CreateScalarAnimation(compositor, 1, 0, duration, easing, 0.82f));
        previewVisual.StartAnimation(nameof(Visual.Opacity), CreateScalarAnimation(compositor, 0, 1, duration, easing, 0.72f));
        toolbarVisual.StartAnimation(nameof(Visual.Opacity), CreateScalarAnimation(compositor, 0, 1, duration, easing, 0.62f));
        backdropVisual.StartAnimation(nameof(Visual.Opacity), CreateScalarAnimation(compositor, 0, 1, duration, easing, 0));

        entranceTimer?.Stop();
        entranceTimer = reviewLayer.DispatcherQueue.CreateTimer();
        entranceTimer.Interval = duration;
        entranceTimer.IsRepeating = false;
        entranceTimer.Tick += HandleEntranceCompleted;
        entranceTimer.Start();
    }

    public void Confirm()
    {
        if (completed || transitioning)
        {
            return;
        }

        transitioning = true;
        StopEntranceTimer();
        Complete(bitmap);
    }

    public void Dismiss()
    {
        if (completed || transitioning)
        {
            return;
        }

        transitioning = true;
        StopEntranceTimer();
        PlayDismissAnimation();

        dismissTimer = reviewLayer.DispatcherQueue.CreateTimer();
        dismissTimer.Interval = TimeSpan.FromMilliseconds(DismissDurationMs);
        dismissTimer.IsRepeating = false;
        dismissTimer.Tick += HandleDismissCompleted;
        dismissTimer.Start();
    }

    public void CancelImmediately()
    {
        StopEntranceTimer();
        dismissTimer?.Stop();
        Complete(null);
    }

    private void HandleEntranceCompleted(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        sender.Tick -= HandleEntranceCompleted;
        entranceTimer = null;
        animationPreview.Visibility = Visibility.Collapsed;
    }

    private void StopEntranceTimer()
    {
        if (entranceTimer is null)
        {
            return;
        }

        entranceTimer.Stop();
        entranceTimer.Tick -= HandleEntranceCompleted;
        entranceTimer = null;
    }

    private void HandleDismissCompleted(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        sender.Tick -= HandleDismissCompleted;
        dismissTimer = null;
        Complete(null);
    }

    private void PlayDismissAnimation()
    {
        Visual previewVisual = ElementCompositionPreview.GetElementVisual(previewHost);
        Visual animationVisual = ElementCompositionPreview.GetElementVisual(animationPreview);
        Visual toolbarVisual = ElementCompositionPreview.GetElementVisual(toolbar);
        Visual backdropVisual = ElementCompositionPreview.GetElementVisual(reviewBackdrop);
        Compositor compositor = previewVisual.Compositor;
        TimeSpan duration = TimeSpan.FromMilliseconds(DismissDurationMs);
        SineEasingFunction easing = CompositionEasingFunction.CreateSineEasingFunction(compositor, CompositionEasingFunctionMode.In);
        float distance = (float)Math.Max(120, availableHeight - PreviewBounds.Y + 48);

        AnimateDown(previewVisual, distance, duration, easing);
        AnimateDown(animationVisual, distance, duration, easing);
        AnimateDown(toolbarVisual, distance, duration, easing);

        backdropVisual.Opacity = 0;
        backdropVisual.StartAnimation(nameof(Visual.Opacity), CreateScalarAnimation(compositor, 1, 0, duration, easing, 0));
    }

    private static void AnimateDown(Visual visual, float distance, TimeSpan duration, CompositionEasingFunction easing)
    {
        Vector3 offset = visual.Offset;
        Vector3 destination = offset + new Vector3(0, distance, 0);
        float opacity = visual.Opacity;

        visual.Offset = destination;
        visual.Opacity = 0;
        visual.StartAnimation(nameof(Visual.Offset), CreateVectorAnimation(visual.Compositor, offset, destination, duration, easing));
        visual.StartAnimation(nameof(Visual.Opacity), CreateScalarAnimation(visual.Compositor, opacity, 0, duration, easing, 0.55f));
    }

    private void Complete(DesktopCaptureBitmap? result)
    {
        if (completed)
        {
            return;
        }

        completed = true;
        completion.TrySetResult(result);
    }

    private static Border CreatePreviewHost(ImageSource imageSource, double width, double height) =>
        new()
        {
            Width = width,
            Height = height,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 20, 28)),
            BorderBrush = ResolveBrush("ControlStrokeColorDefaultBrush", Windows.UI.Color.FromArgb(72, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = new Image
            {
                Source = imageSource,
                Stretch = Stretch.Fill
            },
            Shadow = new ThemeShadow()
        };

    private static Button CreateToolbarButton(string glyph, string label, bool accent)
    {
        Button button = new()
        {
            Width = 36,
            Height = 36,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(18),
            Content = new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 14,
                Glyph = glyph
            }
        };

        if (accent && Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("AccentButtonStyle", out object value) && value is Style style)
        {
            button.Style = style;
        }

        AutomationProperties.SetName(button, label);
        ToolTipService.SetToolTip(button, label);
        return button;
    }

    private static Vector3KeyFrameAnimation CreateVectorAnimation(Compositor compositor, Vector3 from, Vector3 to, TimeSpan duration, CompositionEasingFunction easing)
    {
        Vector3KeyFrameAnimation animation = compositor.CreateVector3KeyFrameAnimation();
        animation.Duration = duration;
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, easing);
        return animation;
    }

    private static ScalarKeyFrameAnimation CreateScalarAnimation(Compositor compositor, float from, float to, TimeSpan duration, CompositionEasingFunction easing, float delayProgress)
    {
        ScalarKeyFrameAnimation animation = compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = duration;
        animation.InsertKeyFrame(0, from);

        if (delayProgress > 0)
        {
            animation.InsertKeyFrame(delayProgress, from);
        }

        animation.InsertKeyFrame(1, to, easing);
        return animation;
    }

    private static WriteableBitmap CreateImageSource(DesktopCaptureBitmap bitmap)
    {
        WriteableBitmap imageSource = new(bitmap.Width, bitmap.Height);
        using Stream stream = imageSource.PixelBuffer.AsStream();
        stream.Write(bitmap.Pixels);
        imageSource.Invalidate();
        return imageSource;
    }

    private static Brush ResolveMicaBrush()
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("MicaBackgroundFillColorBaseAltBrush", out object micaValue) && micaValue is Brush micaBrush)
        {
            return micaBrush;
        }

        return ResolveBrush("LayerFillColorDefaultBrush", Windows.UI.Color.FromArgb(245, 32, 32, 32));
    }

    private static Brush ResolveBrush(string key, Windows.UI.Color fallback)
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out object value) && value is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallback);
    }
}
