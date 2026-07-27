using Glance.Application.Abstractions;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Glance.ScreenCapture.WinUI;

internal sealed class CaptureReviewSurface
{
    private const double HandleHitSize = 22;
    private const double MinimumCropSize = 48;
    private const int ParkDurationMs = 280;
    private readonly DesktopCaptureBitmap bitmap;
    private readonly Border cropBorder;
    private readonly Canvas cropCanvas;
    private readonly RectangleGeometry cropCutout;
    private readonly TaskCompletionSource<DesktopCaptureBitmap?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Border previewSurface;
    private readonly Border reviewBackdrop;
    private readonly Grid reviewLayer;
    private readonly Border toolbar;
    private CropDragMode dragMode;
    private Point lastPointerPosition;
    private Rect cropBounds;
    private bool completed;
    private bool ready;
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
        cropBounds = new Rect(0, 0, previewWidth, previewHeight);

        WriteableBitmap imageSource = CreateImageSource(bitmap);
        Image image = new()
        {
            Source = imageSource,
            Stretch = Stretch.Fill
        };
        RectangleGeometry shadeBounds = new() { Rect = new Rect(0, 0, previewWidth, previewHeight) };
        cropCutout = new RectangleGeometry { Rect = cropBounds };
        GeometryGroup shadeGeometry = new() { FillRule = FillRule.EvenOdd };
        shadeGeometry.Children.Add(shadeBounds);
        shadeGeometry.Children.Add(cropCutout);
        Microsoft.UI.Xaml.Shapes.Path shade = new()
        {
            Data = shadeGeometry,
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(128, 0, 0, 0)),
            IsHitTestVisible = false
        };
        cropBorder = new Border
        {
            BorderBrush = ResolveBrush("GlanceScreenCaptureIconBrush", Windows.UI.Color.FromArgb(255, 104, 216, 255)),
            BorderThickness = new Thickness(2),
            IsHitTestVisible = false
        };
        cropCanvas = new Canvas
        {
            Width = previewWidth,
            Height = previewHeight,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0))
        };
        cropCanvas.Children.Add(shade);
        cropCanvas.Children.Add(cropBorder);

        foreach (Border handle in CreateHandles())
        {
            cropCanvas.Children.Add(handle);
        }

        cropCanvas.PointerPressed += HandlePointerPressed;
        cropCanvas.PointerMoved += HandlePointerMoved;
        cropCanvas.PointerReleased += HandlePointerReleased;
        cropCanvas.PointerCanceled += HandlePointerCanceled;
        cropCanvas.PointerCaptureLost += HandlePointerCaptureLost;

        Grid previewContent = new();
        previewContent.Children.Add(image);
        previewContent.Children.Add(cropCanvas);
        previewSurface = new Border
        {
            Width = previewWidth,
            Height = previewHeight,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 20, 28)),
            BorderBrush = ResolveBrush("ControlStrokeColorDefaultBrush", Windows.UI.Color.FromArgb(72, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = previewContent,
            Shadow = new ThemeShadow()
        };
        previewSurface.Translation = new Vector3(0, 0, 32);
        Canvas.SetLeft(previewSurface, previewX);
        Canvas.SetTop(previewSurface, previewY);

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
        double toolbarWidth = toolbar.DesiredSize.Width;
        double toolbarHeight = toolbar.DesiredSize.Height;
        Canvas.SetLeft(toolbar, Math.Round((availableWidth - toolbarWidth) / 2));
        Canvas.SetTop(toolbar, Math.Max(20, previewY - toolbarHeight - 14));

        Canvas reviewCanvas = new();
        reviewCanvas.Children.Add(previewSurface);
        reviewCanvas.Children.Add(toolbar);
        reviewBackdrop = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(178, 8, 10, 14))
        };
        reviewLayer = new Grid { IsHitTestVisible = false };
        reviewLayer.Children.Add(reviewBackdrop);
        reviewLayer.Children.Add(reviewCanvas);
        UpdateCropChrome();
    }

    public FrameworkElement Content => reviewLayer;

    public Rect PreviewBounds { get; }

    public Rect SelectedPreviewBounds => new(PreviewBounds.X + cropBounds.X, PreviewBounds.Y + cropBounds.Y, cropBounds.Width, cropBounds.Height);

    public Task<DesktopCaptureBitmap?> Completion => completion.Task;

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

    public Task PlayEntranceAsync(Rect sourceBounds)
    {
        Visual previewVisual = ElementCompositionPreview.GetElementVisual(previewSurface);
        Visual toolbarVisual = ElementCompositionPreview.GetElementVisual(toolbar);
        Visual backdropVisual = ElementCompositionPreview.GetElementVisual(reviewBackdrop);
        Compositor compositor = previewVisual.Compositor;
        TimeSpan duration = TimeSpan.FromMilliseconds(ParkDurationMs);
        SineEasingFunction easing = CompositionEasingFunction.CreateSineEasingFunction(compositor, CompositionEasingFunctionMode.Out);
        Vector3 targetCenter = new((float)(PreviewBounds.X + (PreviewBounds.Width / 2)), (float)(PreviewBounds.Y + (PreviewBounds.Height / 2)), 0);
        Vector3 sourceCenter = new((float)(sourceBounds.X + (sourceBounds.Width / 2)), (float)(sourceBounds.Y + (sourceBounds.Height / 2)), 0);
        Vector3 startOffset = sourceCenter - targetCenter;
        Vector3 startScale = new((float)Math.Max(0.01, sourceBounds.Width / PreviewBounds.Width), (float)Math.Max(0.01, sourceBounds.Height / PreviewBounds.Height), 1);
        Vector3 previewCenter = new((float)PreviewBounds.Width / 2, (float)PreviewBounds.Height / 2, 0);
        previewVisual.CenterPoint = previewCenter;

        Vector3KeyFrameAnimation offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
        offsetAnimation.Duration = duration;
        offsetAnimation.InsertKeyFrame(0, startOffset);
        offsetAnimation.InsertKeyFrame(1, Vector3.Zero, easing);

        Vector3KeyFrameAnimation scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.Duration = duration;
        scaleAnimation.InsertKeyFrame(0, startScale);
        scaleAnimation.InsertKeyFrame(1, Vector3.One, easing);

        ScalarKeyFrameAnimation backdropOpacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        backdropOpacityAnimation.Duration = duration;
        backdropOpacityAnimation.InsertKeyFrame(0, 0);
        backdropOpacityAnimation.InsertKeyFrame(0.35f, 1, easing);
        backdropOpacityAnimation.InsertKeyFrame(1, 1);

        ScalarKeyFrameAnimation toolbarOpacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        toolbarOpacityAnimation.Duration = duration;
        toolbarOpacityAnimation.InsertKeyFrame(0, 0);
        toolbarOpacityAnimation.InsertKeyFrame(0.55f, 0);
        toolbarOpacityAnimation.InsertKeyFrame(1, 1, easing);

        Vector3KeyFrameAnimation toolbarOffsetAnimation = compositor.CreateVector3KeyFrameAnimation();
        toolbarOffsetAnimation.Duration = duration;
        toolbarOffsetAnimation.InsertKeyFrame(0, new Vector3(0, -8, 48));
        toolbarOffsetAnimation.InsertKeyFrame(0.55f, new Vector3(0, -8, 48));
        toolbarOffsetAnimation.InsertKeyFrame(1, new Vector3(0, 0, 48), easing);

        TaskCompletionSource<bool> animationCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CompositionScopedBatch batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        previewVisual.Offset = Vector3.Zero;
        previewVisual.Scale = Vector3.One;
        backdropVisual.Opacity = 1;
        toolbarVisual.Opacity = 1;
        previewVisual.StartAnimation(nameof(Visual.Offset), offsetAnimation);
        previewVisual.StartAnimation(nameof(Visual.Scale), scaleAnimation);
        backdropVisual.StartAnimation(nameof(Visual.Opacity), backdropOpacityAnimation);
        toolbarVisual.StartAnimation(nameof(Visual.Opacity), toolbarOpacityAnimation);
        toolbarVisual.StartAnimation(nameof(Visual.Offset), toolbarOffsetAnimation);
        batch.End();
        batch.Completed += (_, _) =>
        {
            batch.Dispose();
            ready = true;
            reviewLayer.IsHitTestVisible = true;
            animationCompletion.TrySetResult(true);
        };
        return animationCompletion.Task;
    }

    public void Detach()
    {
        cropCanvas.PointerPressed -= HandlePointerPressed;
        cropCanvas.PointerMoved -= HandlePointerMoved;
        cropCanvas.PointerReleased -= HandlePointerReleased;
        cropCanvas.PointerCanceled -= HandlePointerCanceled;
        cropCanvas.PointerCaptureLost -= HandlePointerCaptureLost;
    }

    private async Task CompleteAsync(bool confirmed)
    {
        if (completed || transitioning || !ready)
        {
            return;
        }

        transitioning = true;

        try
        {
            await PlayExitAsync(confirmed);
        }
        finally
        {
            completed = true;
            transitioning = false;
            completion.TrySetResult(confirmed ? CreateCroppedBitmap() : null);
        }
    }

    private Task PlayExitAsync(bool confirmed)
    {
        Visual previewVisual = ElementCompositionPreview.GetElementVisual(previewSurface);
        Visual cropVisual = ElementCompositionPreview.GetElementVisual(cropCanvas);
        Visual toolbarVisual = ElementCompositionPreview.GetElementVisual(toolbar);
        Visual backdropVisual = ElementCompositionPreview.GetElementVisual(reviewBackdrop);
        Compositor compositor = previewVisual.Compositor;
        TimeSpan duration = TimeSpan.FromMilliseconds(confirmed ? 150 : 190);
        SineEasingFunction easing = CompositionEasingFunction.CreateSineEasingFunction(compositor, confirmed ? CompositionEasingFunctionMode.Out : CompositionEasingFunctionMode.In);

        ScalarKeyFrameAnimation chromeOpacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        chromeOpacityAnimation.Duration = duration;
        chromeOpacityAnimation.InsertKeyFrame(0, 1);
        chromeOpacityAnimation.InsertKeyFrame(1, 0, easing);

        Vector3KeyFrameAnimation toolbarOffsetAnimation = compositor.CreateVector3KeyFrameAnimation();
        toolbarOffsetAnimation.Duration = duration;
        toolbarOffsetAnimation.InsertKeyFrame(0, new Vector3(0, 0, 48));
        toolbarOffsetAnimation.InsertKeyFrame(1, new Vector3(0, -8, 48), easing);

        CompositionScopedBatch batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        toolbarVisual.Opacity = 0;
        cropVisual.Opacity = 0;
        toolbarVisual.StartAnimation(nameof(Visual.Opacity), chromeOpacityAnimation);
        toolbarVisual.StartAnimation(nameof(Visual.Offset), toolbarOffsetAnimation);
        cropVisual.StartAnimation(nameof(Visual.Opacity), chromeOpacityAnimation);

        if (confirmed)
        {
            InsetClip cropClip = compositor.CreateInsetClip();
            previewVisual.Clip = cropClip;
            StartInsetAnimation(cropClip, nameof(InsetClip.LeftInset), (float)cropBounds.Left, duration, easing);
            StartInsetAnimation(cropClip, nameof(InsetClip.TopInset), (float)cropBounds.Top, duration, easing);
            StartInsetAnimation(cropClip, nameof(InsetClip.RightInset), (float)(PreviewBounds.Width - cropBounds.Right), duration, easing);
            StartInsetAnimation(cropClip, nameof(InsetClip.BottomInset), (float)(PreviewBounds.Height - cropBounds.Bottom), duration, easing);
        }
        else
        {
            ScalarKeyFrameAnimation backdropOpacityAnimation = compositor.CreateScalarKeyFrameAnimation();
            backdropOpacityAnimation.Duration = duration;
            backdropOpacityAnimation.InsertKeyFrame(0, 1);
            backdropOpacityAnimation.InsertKeyFrame(1, 0, easing);

            ScalarKeyFrameAnimation previewOpacityAnimation = compositor.CreateScalarKeyFrameAnimation();
            previewOpacityAnimation.Duration = duration;
            previewOpacityAnimation.InsertKeyFrame(0, 1);
            previewOpacityAnimation.InsertKeyFrame(1, 0, easing);

            Vector3KeyFrameAnimation previewScaleAnimation = compositor.CreateVector3KeyFrameAnimation();
            previewScaleAnimation.Duration = duration;
            previewScaleAnimation.InsertKeyFrame(0, Vector3.One);
            previewScaleAnimation.InsertKeyFrame(1, new Vector3(0.94f, 0.94f, 1), easing);
            previewVisual.Opacity = 0;
            previewVisual.Scale = new Vector3(0.94f, 0.94f, 1);
            backdropVisual.Opacity = 0;
            previewVisual.StartAnimation(nameof(Visual.Opacity), previewOpacityAnimation);
            previewVisual.StartAnimation(nameof(Visual.Scale), previewScaleAnimation);
            backdropVisual.StartAnimation(nameof(Visual.Opacity), backdropOpacityAnimation);
        }

        batch.End();
        TaskCompletionSource<bool> animationCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        batch.Completed += (_, _) =>
        {
            batch.Dispose();
            animationCompletion.TrySetResult(true);
        };
        return animationCompletion.Task;
    }

    private static void StartInsetAnimation(InsetClip clip, string propertyName, float inset, TimeSpan duration, CompositionEasingFunction easing)
    {
        Compositor compositor = clip.Compositor;
        ScalarKeyFrameAnimation animation = compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = duration;
        animation.InsertKeyFrame(0, 0);
        animation.InsertKeyFrame(1, inset, easing);
        clip.StartAnimation(propertyName, animation);
    }

    private DesktopCaptureBitmap CreateCroppedBitmap()
    {
        double scaleX = bitmap.Width / PreviewBounds.Width;
        double scaleY = bitmap.Height / PreviewBounds.Height;
        int left = Math.Clamp((int)Math.Round(cropBounds.X * scaleX), 0, bitmap.Width - 1);
        int top = Math.Clamp((int)Math.Round(cropBounds.Y * scaleY), 0, bitmap.Height - 1);
        int right = Math.Clamp((int)Math.Round(cropBounds.Right * scaleX), left + 1, bitmap.Width);
        int bottom = Math.Clamp((int)Math.Round(cropBounds.Bottom * scaleY), top + 1, bitmap.Height);
        return bitmap.Crop(new NativeRectangle(bitmap.OriginX + left, bitmap.OriginY + top, right - left, bottom - top));
    }

    private void HandlePointerPressed(object sender, PointerRoutedEventArgs args)
    {
        Point position = args.GetCurrentPoint(cropCanvas).Position;
        dragMode = ResolveDragMode(position);

        if (dragMode == CropDragMode.None)
        {
            return;
        }

        lastPointerPosition = position;
        cropCanvas.CapturePointer(args.Pointer);
        args.Handled = true;
    }

    private void HandlePointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (dragMode == CropDragMode.None)
        {
            return;
        }

        Point position = args.GetCurrentPoint(cropCanvas).Position;
        double deltaX = position.X - lastPointerPosition.X;
        double deltaY = position.Y - lastPointerPosition.Y;
        lastPointerPosition = position;
        Rect updated = cropBounds;

        if (dragMode == CropDragMode.Move)
        {
            updated.X = Math.Clamp(updated.X + deltaX, 0, cropCanvas.Width - updated.Width);
            updated.Y = Math.Clamp(updated.Y + deltaY, 0, cropCanvas.Height - updated.Height);
        }
        else
        {
            double minimumWidth = Math.Min(MinimumCropSize, cropCanvas.Width);
            double minimumHeight = Math.Min(MinimumCropSize, cropCanvas.Height);

            if (dragMode.HasFlag(CropDragMode.Left))
            {
                double right = updated.Right;
                updated.X = Math.Clamp(updated.X + deltaX, 0, right - minimumWidth);
                updated.Width = right - updated.X;
            }

            if (dragMode.HasFlag(CropDragMode.Right))
            {
                updated.Width = Math.Clamp(updated.Width + deltaX, minimumWidth, cropCanvas.Width - updated.X);
            }

            if (dragMode.HasFlag(CropDragMode.Top))
            {
                double bottom = updated.Bottom;
                updated.Y = Math.Clamp(updated.Y + deltaY, 0, bottom - minimumHeight);
                updated.Height = bottom - updated.Y;
            }

            if (dragMode.HasFlag(CropDragMode.Bottom))
            {
                updated.Height = Math.Clamp(updated.Height + deltaY, minimumHeight, cropCanvas.Height - updated.Y);
            }
        }

        cropBounds = updated;
        UpdateCropChrome();
        args.Handled = true;
    }

    private void HandlePointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (dragMode == CropDragMode.None)
        {
            return;
        }

        cropCanvas.ReleasePointerCapture(args.Pointer);
        dragMode = CropDragMode.None;
        args.Handled = true;
    }

    private void HandlePointerCanceled(object sender, PointerRoutedEventArgs args) =>
        dragMode = CropDragMode.None;

    private void HandlePointerCaptureLost(object sender, PointerRoutedEventArgs args) =>
        dragMode = CropDragMode.None;

    private CropDragMode ResolveDragMode(Point point)
    {
        bool nearLeft = Math.Abs(point.X - cropBounds.Left) <= HandleHitSize;
        bool nearRight = Math.Abs(point.X - cropBounds.Right) <= HandleHitSize;
        bool nearTop = Math.Abs(point.Y - cropBounds.Top) <= HandleHitSize;
        bool nearBottom = Math.Abs(point.Y - cropBounds.Bottom) <= HandleHitSize;
        bool withinHorizontalRange = point.X >= cropBounds.Left - HandleHitSize && point.X <= cropBounds.Right + HandleHitSize;
        bool withinVerticalRange = point.Y >= cropBounds.Top - HandleHitSize && point.Y <= cropBounds.Bottom + HandleHitSize;
        CropDragMode mode = CropDragMode.None;

        if (nearLeft && withinVerticalRange)
        {
            mode |= CropDragMode.Left;
        }
        else if (nearRight && withinVerticalRange)
        {
            mode |= CropDragMode.Right;
        }

        if (nearTop && withinHorizontalRange)
        {
            mode |= CropDragMode.Top;
        }
        else if (nearBottom && withinHorizontalRange)
        {
            mode |= CropDragMode.Bottom;
        }

        if (mode == CropDragMode.None && cropBounds.Contains(point))
        {
            return CropDragMode.Move;
        }

        return mode;
    }

    private void UpdateCropChrome()
    {
        cropCutout.Rect = cropBounds;
        Canvas.SetLeft(cropBorder, cropBounds.X);
        Canvas.SetTop(cropBorder, cropBounds.Y);
        cropBorder.Width = cropBounds.Width;
        cropBorder.Height = cropBounds.Height;

        foreach (UIElement child in cropCanvas.Children)
        {
            if (child is Border { Tag: CropHandlePosition position } handle)
            {
                PositionHandle(handle, position);
            }
        }
    }

    private IEnumerable<Border> CreateHandles()
    {
        yield return CreateHandle(CropHandlePosition.TopLeft);
        yield return CreateHandle(CropHandlePosition.Top);
        yield return CreateHandle(CropHandlePosition.TopRight);
        yield return CreateHandle(CropHandlePosition.Right);
        yield return CreateHandle(CropHandlePosition.BottomRight);
        yield return CreateHandle(CropHandlePosition.Bottom);
        yield return CreateHandle(CropHandlePosition.BottomLeft);
        yield return CreateHandle(CropHandlePosition.Left);
    }

    private static Border CreateHandle(CropHandlePosition position) =>
        new()
        {
            Width = position is CropHandlePosition.Top or CropHandlePosition.Bottom ? 22 : 10,
            Height = position is CropHandlePosition.Left or CropHandlePosition.Right ? 22 : 10,
            Background = ResolveBrush("GlanceScreenCaptureIconBrush", Windows.UI.Color.FromArgb(255, 104, 216, 255)),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(230, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            IsHitTestVisible = false,
            Tag = position
        };

    private void PositionHandle(Border handle, CropHandlePosition position)
    {
        double left = position switch
        {
            CropHandlePosition.TopLeft or CropHandlePosition.Left or CropHandlePosition.BottomLeft => cropBounds.Left - (handle.Width / 2),
            CropHandlePosition.Top or CropHandlePosition.Bottom => cropBounds.Left + ((cropBounds.Width - handle.Width) / 2),
            _ => cropBounds.Right - (handle.Width / 2)
        };
        double top = position switch
        {
            CropHandlePosition.TopLeft or CropHandlePosition.Top or CropHandlePosition.TopRight => cropBounds.Top - (handle.Height / 2),
            CropHandlePosition.Left or CropHandlePosition.Right => cropBounds.Top + ((cropBounds.Height - handle.Height) / 2),
            _ => cropBounds.Bottom - (handle.Height / 2)
        };
        Canvas.SetLeft(handle, Math.Clamp(left, 0, cropCanvas.Width - handle.Width));
        Canvas.SetTop(handle, Math.Clamp(top, 0, cropCanvas.Height - handle.Height));
    }

    private static Button CreateToolbarButton(string glyph, string label, bool accent)
    {
        FontIcon icon = new()
        {
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 14,
            Glyph = glyph
        };
        Button button = new()
        {
            Width = 36,
            Height = 36,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(18),
            Content = icon
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

    [Flags]
    private enum CropDragMode
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8,
        Move = 16
    }

    private enum CropHandlePosition
    {
        TopLeft,
        Top,
        TopRight,
        Right,
        BottomRight,
        Bottom,
        BottomLeft,
        Left
    }
}
