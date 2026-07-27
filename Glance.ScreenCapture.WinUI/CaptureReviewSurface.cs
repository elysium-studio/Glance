using CommunityToolkit.WinUI.Controls;
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
    private const int ConfirmDurationMs = 150;
    private const int DismissDurationMs = 190;
    private const int ParkDurationMs = 280;
    private readonly DesktopCaptureBitmap bitmap;
    private readonly Border previewHost;
    private readonly ImageCropper cropper;
    private readonly Border reviewBackdrop;
    private readonly Grid reviewLayer;
    private readonly Border toolbar;
    private readonly TaskCompletionSource<DesktopCaptureBitmap?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool completed;
    private bool transitioning;

    public CaptureReviewSurface(DesktopCaptureBitmap bitmap, ITextLocalizer localizer, double availableWidth, double availableHeight)
    {
        this.bitmap = bitmap;

        double maximumWidth = Math.Max(240, availableWidth - 96);
        double maximumHeight = Math.Max(160, availableHeight - 180);
        double scale = Math.Min(1, Math.Min(maximumWidth / bitmap.Width, maximumHeight / bitmap.Height));
        double previewWidth = Math.Max(1, Math.Round(bitmap.Width * scale));
        double previewHeight = Math.Max(1, Math.Round(bitmap.Height * scale));
        double previewX = Math.Round((availableWidth - previewWidth) / 2);
        double previewY = Math.Round((availableHeight - previewHeight + 42) / 2);
        PreviewBounds = new Rect(previewX, previewY, previewWidth, previewHeight);

        cropper = new ImageCropper
        {
            Width = previewWidth,
            Height = previewHeight,
            AspectRatio = null,
            CropShape = CropShape.Rectangular,
            MinCroppedPixelLength = Math.Min(48, Math.Min(bitmap.Width, bitmap.Height)),
            MinSelectedLength = Math.Min(48, Math.Min(previewWidth, previewHeight)),
            Source = CreateImageSource(bitmap),
            ThumbPlacement = ThumbPlacement.All
        };
        previewHost = new Border
        {
            Width = previewWidth,
            Height = previewHeight,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 20, 28)),
            BorderBrush = ResolveBrush("ControlStrokeColorDefaultBrush", Windows.UI.Color.FromArgb(72, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = cropper,
            Shadow = new ThemeShadow()
        };
        previewHost.Translation = new Vector3(0, 0, 32);
        Canvas.SetLeft(previewHost, previewX);
        Canvas.SetTop(previewHost, previewY);

        Button dismissButton = CreateToolbarButton("\uE711", localizer.GetText("DismissCapture"), false);
        dismissButton.Click += (_, _) => Dismiss();
        Button confirmButton = CreateToolbarButton("\uE73E", localizer.GetText("ConfirmCapture"), true);
        confirmButton.Click += (_, _) => Confirm();
        TextBlock title = new()
        {
            Margin = new Thickness(4, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Style = ResolveStyle("BodyStrongTextBlockStyle"),
            Text = localizer.GetText("CropCapture")
        };
        StackPanel toolbarContent = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };
        toolbarContent.Children.Add(dismissButton);
        toolbarContent.Children.Add(title);
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
        Canvas.SetLeft(toolbar, Math.Round((availableWidth - toolbar.DesiredSize.Width) / 2));
        Canvas.SetTop(toolbar, Math.Max(20, previewY - toolbar.DesiredSize.Height - 14));

        reviewBackdrop = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(178, 8, 10, 14)),
            IsHitTestVisible = false
        };
        Canvas reviewCanvas = new();
        reviewCanvas.Children.Add(previewHost);
        reviewCanvas.Children.Add(toolbar);
        reviewLayer = new Grid();
        reviewLayer.Children.Add(reviewBackdrop);
        reviewLayer.Children.Add(reviewCanvas);
    }

    public FrameworkElement Content => reviewLayer;

    public Task<DesktopCaptureBitmap?> Completion => completion.Task;

    public Rect PreviewBounds { get; }

    public Rect SelectedPreviewBounds
    {
        get
        {
            Rect region = cropper.CroppedRegion;
            double scaleX = PreviewBounds.Width / bitmap.Width;
            double scaleY = PreviewBounds.Height / bitmap.Height;
            return new Rect(PreviewBounds.X + (region.X * scaleX), PreviewBounds.Y + (region.Y * scaleY), region.Width * scaleX, region.Height * scaleY);
        }
    }

    public void PlayEntrance(Rect sourceBounds)
    {
        Visual previewVisual = ElementCompositionPreview.GetElementVisual(previewHost);
        Visual toolbarVisual = ElementCompositionPreview.GetElementVisual(toolbar);
        Visual backdropVisual = ElementCompositionPreview.GetElementVisual(reviewBackdrop);
        Vector3 targetCenter = new((float)(PreviewBounds.X + (PreviewBounds.Width / 2)), (float)(PreviewBounds.Y + (PreviewBounds.Height / 2)), 0);
        Vector3 sourceCenter = new((float)(sourceBounds.X + (sourceBounds.Width / 2)), (float)(sourceBounds.Y + (sourceBounds.Height / 2)), 0);
        previewVisual.CenterPoint = new Vector3((float)PreviewBounds.Width / 2, (float)PreviewBounds.Height / 2, 0);
        previewVisual.Offset = sourceCenter - targetCenter;
        previewVisual.Scale = new Vector3((float)Math.Max(0.01, sourceBounds.Width / PreviewBounds.Width), (float)Math.Max(0.01, sourceBounds.Height / PreviewBounds.Height), 1);
        toolbarVisual.Offset = new Vector3(0, -8, 48);
        toolbarVisual.Opacity = 0;
        backdropVisual.Opacity = 0;

        void Start()
        {
            if (completed)
            {
                return;
            }

            Compositor compositor = previewVisual.Compositor;
            TimeSpan duration = TimeSpan.FromMilliseconds(ParkDurationMs);
            SineEasingFunction easing = CompositionEasingFunction.CreateSineEasingFunction(compositor, CompositionEasingFunctionMode.Out);
            previewVisual.Offset = Vector3.Zero;
            previewVisual.Scale = Vector3.One;
            toolbarVisual.Offset = new Vector3(0, 0, 48);
            toolbarVisual.Opacity = 1;
            backdropVisual.Opacity = 1;
            previewVisual.StartAnimation(nameof(Visual.Offset), CreateVectorAnimation(compositor, sourceCenter - targetCenter, Vector3.Zero, duration, easing));
            previewVisual.StartAnimation(nameof(Visual.Scale), CreateVectorAnimation(compositor, new Vector3((float)Math.Max(0.01, sourceBounds.Width / PreviewBounds.Width), (float)Math.Max(0.01, sourceBounds.Height / PreviewBounds.Height), 1), Vector3.One, duration, easing));
            toolbarVisual.StartAnimation(nameof(Visual.Offset), CreateVectorAnimation(compositor, new Vector3(0, -8, 48), new Vector3(0, 0, 48), duration, easing, 0.55f));
            toolbarVisual.StartAnimation(nameof(Visual.Opacity), CreateScalarAnimation(compositor, 0, 1, duration, easing, 0.55f));
            backdropVisual.StartAnimation(nameof(Visual.Opacity), CreateScalarAnimation(compositor, 0, 1, duration, easing, 0));
        }

        DispatcherQueue dispatcherQueue = previewHost.DispatcherQueue;

        if (!dispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, Start))
        {
            previewVisual.Offset = Vector3.Zero;
            previewVisual.Scale = Vector3.One;
            toolbarVisual.Offset = new Vector3(0, 0, 48);
            toolbarVisual.Opacity = 1;
            backdropVisual.Opacity = 1;
        }
    }

    public void Confirm()
    {
        _ = CompleteAsync(true);
    }

    public void Dismiss()
    {
        _ = CompleteAsync(false);
    }

    public void CancelImmediately()
    {
        if (completed)
        {
            return;
        }

        completed = true;
        completion.TrySetResult(null);
    }

    public void Detach()
    {
        try
        {
            cropper.ReleasePointerCaptures();
            Visual previewVisual = ElementCompositionPreview.GetElementVisual(previewHost);
            Visual toolbarVisual = ElementCompositionPreview.GetElementVisual(toolbar);
            Visual backdropVisual = ElementCompositionPreview.GetElementVisual(reviewBackdrop);
            previewVisual.StopAnimation(nameof(Visual.Offset));
            previewVisual.StopAnimation(nameof(Visual.Scale));
            previewVisual.StopAnimation(nameof(Visual.Opacity));
            toolbarVisual.StopAnimation(nameof(Visual.Offset));
            toolbarVisual.StopAnimation(nameof(Visual.Opacity));
            backdropVisual.StopAnimation(nameof(Visual.Opacity));
        }
        catch
        {
        }
    }

    private async Task CompleteAsync(bool confirmed)
    {
        if (completed || transitioning)
        {
            return;
        }

        transitioning = true;
        DesktopCaptureBitmap? result = confirmed ? CreateCroppedBitmap() : null;
        PlayExit(confirmed);
        await Task.Delay(confirmed ? ConfirmDurationMs : DismissDurationMs);
        completed = true;
        transitioning = false;
        completion.TrySetResult(result);
    }

    private void PlayExit(bool confirmed)
    {
        Visual previewVisual = ElementCompositionPreview.GetElementVisual(previewHost);
        Visual toolbarVisual = ElementCompositionPreview.GetElementVisual(toolbar);
        Visual backdropVisual = ElementCompositionPreview.GetElementVisual(reviewBackdrop);
        Compositor compositor = previewVisual.Compositor;
        TimeSpan duration = TimeSpan.FromMilliseconds(confirmed ? ConfirmDurationMs : DismissDurationMs);
        SineEasingFunction easing = CompositionEasingFunction.CreateSineEasingFunction(compositor, confirmed ? CompositionEasingFunctionMode.Out : CompositionEasingFunctionMode.In);
        toolbarVisual.Opacity = 0;
        toolbarVisual.Offset = new Vector3(0, -8, 48);
        toolbarVisual.StartAnimation(nameof(Visual.Opacity), CreateScalarAnimation(compositor, 1, 0, duration, easing, 0));
        toolbarVisual.StartAnimation(nameof(Visual.Offset), CreateVectorAnimation(compositor, new Vector3(0, 0, 48), new Vector3(0, -8, 48), duration, easing));

        if (confirmed)
        {
            previewVisual.Scale = new Vector3(0.985f, 0.985f, 1);
            previewVisual.StartAnimation(nameof(Visual.Scale), CreateVectorAnimation(compositor, Vector3.One, new Vector3(0.985f, 0.985f, 1), duration, easing));
            return;
        }

        previewVisual.Opacity = 0;
        previewVisual.Scale = new Vector3(0.94f, 0.94f, 1);
        backdropVisual.Opacity = 0;
        previewVisual.StartAnimation(nameof(Visual.Opacity), CreateScalarAnimation(compositor, 1, 0, duration, easing, 0));
        previewVisual.StartAnimation(nameof(Visual.Scale), CreateVectorAnimation(compositor, Vector3.One, new Vector3(0.94f, 0.94f, 1), duration, easing));
        backdropVisual.StartAnimation(nameof(Visual.Opacity), CreateScalarAnimation(compositor, 1, 0, duration, easing, 0));
    }

    private DesktopCaptureBitmap CreateCroppedBitmap()
    {
        Rect region = cropper.CroppedRegion;
        int left = Math.Clamp((int)Math.Round(region.X), 0, bitmap.Width - 1);
        int top = Math.Clamp((int)Math.Round(region.Y), 0, bitmap.Height - 1);
        int right = Math.Clamp((int)Math.Round(region.Right), left + 1, bitmap.Width);
        int bottom = Math.Clamp((int)Math.Round(region.Bottom), top + 1, bitmap.Height);
        return bitmap.Crop(new NativeRectangle(bitmap.OriginX + left, bitmap.OriginY + top, right - left, bottom - top));
    }

    private static Vector3KeyFrameAnimation CreateVectorAnimation(Compositor compositor, Vector3 from, Vector3 to, TimeSpan duration, CompositionEasingFunction easing, float delayProgress = 0)
    {
        Vector3KeyFrameAnimation animation = compositor.CreateVector3KeyFrameAnimation();
        animation.Duration = duration;
        animation.InsertKeyFrame(0, from);

        if (delayProgress > 0)
        {
            animation.InsertKeyFrame(delayProgress, from);
        }

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

    private static Style? ResolveStyle(string key) =>
        Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out object value) ? value as Style : null;
}
