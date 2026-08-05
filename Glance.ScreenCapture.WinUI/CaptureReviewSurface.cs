using Glance.Application.Abstractions;
using Glance.UI.WinUI;
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
    private const int EntranceDurationMs = 360;
    private readonly Border animationPreview;
    private readonly double availableHeight;
    private readonly double availableWidth;
    private readonly DesktopCaptureBitmap bitmap;
    private readonly TaskCompletionSource<DesktopCaptureBitmap?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ResizableRegionOverlay cropOverlay;
    private readonly Border previewHost;
    private readonly Border reviewBackdrop;
    private readonly Grid reviewLayer;
    private readonly Border toolbar;
    private DispatcherQueueTimer? dismissTimer;
    private DispatcherQueueTimer? entranceTimer;
    private EventHandler<object>? entranceRenderingHandler;
    private bool completed;
    private bool transitioning;

    public CaptureReviewSurface(DesktopCaptureBitmap bitmap, ITextLocalizer localizer, double availableWidth, double availableHeight)
    {
        this.bitmap = bitmap;
        this.availableWidth = availableWidth;
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
        Image previewImage = new()
        {
            Source = imageSource,
            Stretch = Stretch.Fill
        };
        cropOverlay = new ResizableRegionOverlay(previewWidth, previewHeight, bitmap.Width, bitmap.Height);
        cropOverlay.BoundsChanged += HandleCropBoundsChanged;
        cropOverlay.InteractionCompleted += HandleCropInteractionCompleted;
        cropOverlay.InteractionStarted += HandleCropInteractionStarted;
        previewHost = CreatePreviewHost(previewImage, previewWidth, previewHeight);
        ElementCompositionPreview.SetIsTranslationEnabled(previewHost, true);
        previewHost.Translation = new Vector3(0, 0, 32);

        Canvas.SetLeft(previewHost, previewX);
        Canvas.SetTop(previewHost, previewY);
        Canvas.SetLeft(cropOverlay, previewX - ResizableRegionOverlay.VisualPadding);
        Canvas.SetTop(cropOverlay, previewY - ResizableRegionOverlay.VisualPadding);

        animationPreview = CreatePreviewHost(new Image
        {
            Source = imageSource,
            Stretch = Stretch.Fill
        }, previewWidth, previewHeight);
        ElementCompositionPreview.SetIsTranslationEnabled(animationPreview, true);
        animationPreview.Translation = new Vector3(0, 0, 40);
        animationPreview.IsHitTestVisible = false;
        animationPreview.BorderThickness = new Thickness(0);

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
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = OverlayChrome.CreateAcrylicBrush(),
            BorderBrush = ResolveBrush("SurfaceStrokeColorDefaultBrush", Windows.UI.Color.FromArgb(48, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            Child = toolbarContent
        };
        OverlayChrome.Elevate(toolbar, 48);
        PositionToolbar(PreviewBounds);

        reviewBackdrop = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(178, 8, 10, 14)),
            IsHitTestVisible = false
        };

        Canvas reviewCanvas = new();

        reviewCanvas.Children.Add(previewHost);
        reviewCanvas.Children.Add(animationPreview);
        reviewCanvas.Children.Add(cropOverlay);

        reviewLayer = new Grid
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0)),
            IsTabStop = true
        };

        reviewLayer.Children.Add(reviewBackdrop);
        reviewLayer.Children.Add(reviewCanvas);
        reviewLayer.Children.Add(toolbar);
    }

    public FrameworkElement Content => reviewLayer;

    public Task<DesktopCaptureBitmap?> Completion => completion.Task;

    public Rect PreviewBounds { get; }

    public Rect SelectedPreviewBounds
    {
        get
        {
            Rect crop = cropOverlay.CropBounds;
            return new Rect(PreviewBounds.X + crop.X, PreviewBounds.Y + crop.Y, crop.Width, crop.Height);
        }
    }

    public void Focus() => _ = reviewLayer.Focus(FocusState.Programmatic);

    public void PlayEntrance(Rect sourceBounds)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(animationPreview, true);

        Visual animationVisual = ElementCompositionPreview.GetElementVisual(animationPreview);
        Visual previewVisual = ElementCompositionPreview.GetElementVisual(previewHost);
        Visual cropVisual = ElementCompositionPreview.GetElementVisual(cropOverlay);
        Visual backdropVisual = ElementCompositionPreview.GetElementVisual(reviewBackdrop);
        Vector3 targetTranslation = animationPreview.Translation;
        Vector3 sourceCenter = new((float)(sourceBounds.X + (sourceBounds.Width / 2)), (float)(sourceBounds.Y + (sourceBounds.Height / 2)), 0);
        Vector3 targetCenter = new((float)(PreviewBounds.X + (PreviewBounds.Width / 2)), (float)(PreviewBounds.Y + (PreviewBounds.Height / 2)), 0);
        Vector3 sourceTranslation = targetTranslation + sourceCenter - targetCenter;
        Vector3 sourceScale = new((float)Math.Max(0.01, sourceBounds.Width / PreviewBounds.Width), (float)Math.Max(0.01, sourceBounds.Height / PreviewBounds.Height), 1);

        animationVisual.CenterPoint = new Vector3((float)PreviewBounds.Width / 2, (float)PreviewBounds.Height / 2, 0);
        animationPreview.Translation = sourceTranslation;
        animationVisual.Scale = sourceScale;
        animationVisual.Opacity = 1;
        previewVisual.Opacity = 0;
        cropVisual.Opacity = 0;
        cropOverlay.IsHitTestVisible = false;
        backdropVisual.Opacity = 0;

        int preparationFrames = 0;
        entranceRenderingHandler = (_, _) =>
        {
            preparationFrames++;

            if (preparationFrames < 2)
            {
                return;
            }

            CompositionTarget.Rendering -= entranceRenderingHandler;
            entranceRenderingHandler = null;
            StartEntranceAnimations(animationVisual, previewVisual, cropVisual, backdropVisual, targetTranslation, sourceTranslation, sourceScale);
        };

        CompositionTarget.Rendering += entranceRenderingHandler;
    }

    public void Confirm()
    {
        if (completed || transitioning)
        {
            return;
        }

        transitioning = true;
        StopEntrance();
        Complete(CreateCroppedBitmap());
    }

    public void Dismiss()
    {
        if (completed || transitioning)
        {
            return;
        }

        transitioning = true;
        StopEntrance();
        PlayDismissAnimation();

        dismissTimer = reviewLayer.DispatcherQueue.CreateTimer();
        dismissTimer.Interval = TimeSpan.FromMilliseconds(DismissDurationMs);
        dismissTimer.IsRepeating = false;
        dismissTimer.Tick += HandleDismissCompleted;
        dismissTimer.Start();
    }

    public void CancelImmediately()
    {
        StopEntrance();
        dismissTimer?.Stop();
        Complete(null);
    }

    private void HandleEntranceCompleted(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        sender.Tick -= HandleEntranceCompleted;
        entranceTimer = null;
        animationPreview.Visibility = Visibility.Collapsed;
        cropOverlay.IsHitTestVisible = true;
    }

    private void HandleCropBoundsChanged(object? sender, EventArgs args)
    {
        if (toolbar.Visibility == Visibility.Visible)
        {
            PositionToolbar(SelectedPreviewBounds);
        }
    }

    private void HandleCropInteractionStarted(object? sender, EventArgs args) => toolbar.Visibility = Visibility.Collapsed;

    private void HandleCropInteractionCompleted(object? sender, EventArgs args)
    {
        PositionToolbar(SelectedPreviewBounds);
        toolbar.Visibility = Visibility.Visible;
    }

    private void PositionToolbar(Rect selection)
    {
        toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double width = toolbar.DesiredSize.Width;
        double height = toolbar.DesiredSize.Height;
        double left = Math.Clamp(selection.X + ((selection.Width - width) / 2),
            20,
            Math.Max(20, availableWidth - width - 20));
        double top = selection.Y - height - 14;

        if (top < 20)
        {
            top = selection.Bottom + 14;
        }

        if (top + height > availableHeight - 20)
        {
            top = Math.Max(20, selection.Y + 14);
        }

        toolbar.Margin = new Thickness(left, top, 0, 0);
    }

    private void StopEntrance()
    {
        if (entranceRenderingHandler is not null)
        {
            CompositionTarget.Rendering -= entranceRenderingHandler;
            entranceRenderingHandler = null;
        }

        if (entranceTimer is null)
        {
            cropOverlay.IsHitTestVisible = true;
            return;
        }

        entranceTimer.Stop();
        entranceTimer.Tick -= HandleEntranceCompleted;
        entranceTimer = null;
        cropOverlay.IsHitTestVisible = true;
    }

    private void StartEntranceAnimations(Visual animationVisual, Visual previewVisual, Visual cropVisual, Visual backdropVisual, Vector3 targetTranslation, Vector3 sourceTranslation, Vector3 sourceScale)
    {
        Compositor compositor = animationVisual.Compositor;
        TimeSpan duration = TimeSpan.FromMilliseconds(EntranceDurationMs);
        CubicBezierEasingFunction travelEasing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 0.84f), new Vector2(0.28f, 1));
        SineEasingFunction fadeEasing = CompositionEasingFunction.CreateSineEasingFunction(compositor, CompositionEasingFunctionMode.Out);

        Vector3KeyFrameAnimation translationAnimation = compositor.CreateVector3KeyFrameAnimation();
        translationAnimation.Duration = duration;
        translationAnimation.InsertKeyFrame(0, sourceTranslation);
        translationAnimation.InsertKeyFrame(1, targetTranslation, travelEasing);

        Vector3KeyFrameAnimation scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.Duration = duration;
        scaleAnimation.InsertKeyFrame(0, sourceScale);
        scaleAnimation.InsertKeyFrame(1, Vector3.One, travelEasing);

        animationPreview.Translation = targetTranslation;
        animationVisual.Scale = Vector3.One;
        animationVisual.Opacity = 0;
        previewVisual.Opacity = 1;
        cropVisual.Opacity = 1;
        backdropVisual.Opacity = 1;

        animationVisual.StartAnimation("Translation", translationAnimation);
        animationVisual.StartAnimation(nameof(Visual.Scale), scaleAnimation);
        animationVisual.StartAnimation(nameof(Visual.Opacity), CreateScalarAnimation(compositor, 1, 0, duration, fadeEasing, 0.9f));
        previewVisual.StartAnimation(nameof(Visual.Opacity), CreateScalarAnimation(compositor, 0, 1, duration, fadeEasing, 0.86f));
        cropVisual.StartAnimation(nameof(Visual.Opacity), CreateScalarAnimation(compositor, 0, 1, duration, fadeEasing, 0.86f));
        backdropVisual.StartAnimation(nameof(Visual.Opacity), CreateScalarAnimation(compositor, 0, 1, duration, fadeEasing, 0));

        entranceTimer = reviewLayer.DispatcherQueue.CreateTimer();
        entranceTimer.Interval = duration;
        entranceTimer.IsRepeating = false;
        entranceTimer.Tick += HandleEntranceCompleted;
        entranceTimer.Start();
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
        Visual cropVisual = ElementCompositionPreview.GetElementVisual(cropOverlay);
        Visual toolbarVisual = ElementCompositionPreview.GetElementVisual(toolbar);
        Visual backdropVisual = ElementCompositionPreview.GetElementVisual(reviewBackdrop);
        Compositor compositor = previewVisual.Compositor;
        TimeSpan duration = TimeSpan.FromMilliseconds(DismissDurationMs);
        SineEasingFunction easing = CompositionEasingFunction.CreateSineEasingFunction(compositor, CompositionEasingFunctionMode.In);
        float distance = (float)Math.Max(120, availableHeight - PreviewBounds.Y + 48);

        AnimateDown(previewVisual, distance, duration, easing);
        AnimateDown(animationVisual, distance, duration, easing);
        AnimateDown(cropVisual, distance, duration, easing);
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
        cropOverlay.BoundsChanged -= HandleCropBoundsChanged;
        cropOverlay.InteractionCompleted -= HandleCropInteractionCompleted;
        cropOverlay.InteractionStarted -= HandleCropInteractionStarted;
        _ = completion.TrySetResult(result);
    }

    private DesktopCaptureBitmap CreateCroppedBitmap()
    {
        Rect crop = cropOverlay.CropBounds;
        double scaleX = bitmap.Width / PreviewBounds.Width;
        double scaleY = bitmap.Height / PreviewBounds.Height;
        int left = Math.Clamp((int)Math.Floor(crop.X * scaleX), 0, bitmap.Width - 1);
        int top = Math.Clamp((int)Math.Floor(crop.Y * scaleY), 0, bitmap.Height - 1);
        int right = Math.Clamp((int)Math.Ceiling(crop.Right * scaleX), left + 1, bitmap.Width);
        int bottom = Math.Clamp((int)Math.Ceiling(crop.Bottom * scaleY), top + 1, bitmap.Height);

        return left == 0 && top == 0 && right == bitmap.Width && bottom == bitmap.Height
            ? bitmap
            : bitmap.Crop(new NativeRectangle(bitmap.OriginX + left, bitmap.OriginY + top, right - left, bottom - top));
    }

    private static Border CreatePreviewHost(UIElement content, double width, double height) => new()
    {
        Width = width,
        Height = height,
        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 20, 28)),
        BorderBrush = ResolveBrush("ControlStrokeColorDefaultBrush", Windows.UI.Color.FromArgb(72, 255, 255, 255)),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Child = content,
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

    private static Brush ResolveBrush(string key, Windows.UI.Color fallback) => Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out object value) && value is Brush brush
            ? brush
            : new SolidColorBrush(fallback);
}
