using Glance.Application.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
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
    private readonly DesktopCaptureBitmap bitmap;
    private readonly TaskCompletionSource<DesktopCaptureBitmap?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Grid reviewLayer;
    private bool completed;

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

        Image preview = new()
        {
            Source = CreateImageSource(bitmap),
            Stretch = Stretch.Fill
        };

        Border previewHost = new()
        {
            Width = previewWidth,
            Height = previewHeight,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 20, 28)),
            BorderBrush = ResolveBrush("ControlStrokeColorDefaultBrush", Windows.UI.Color.FromArgb(72, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = preview,
            Shadow = new ThemeShadow()
        };

        previewHost?.Translation = new Vector3(0, 0, 32);
        Canvas.SetLeft(previewHost, previewX);
        Canvas.SetTop(previewHost, previewY);

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

        Border toolbar = new()
        {
            Padding = new Thickness(6),
            Background = ResolveMicaBrush(),
            BorderBrush = ResolveBrush("SurfaceStrokeColorDefaultBrush", Windows.UI.Color.FromArgb(48, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            Child = toolbarContent,
            Shadow = new ThemeShadow()
        };

        toolbar?.Translation = new Vector3(0, 0, 48);
        toolbar?.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        double toolbarWidth = toolbar?.DesiredSize.Width ?? 0;
        double toolbarHeight = toolbar?.DesiredSize.Height ?? 0;

        Canvas.SetLeft(toolbar, Math.Round((availableWidth - toolbarWidth) / 2));
        Canvas.SetTop(toolbar, Math.Max(20, previewY - toolbarHeight - 14));

        Border reviewBackdrop = new()
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(178, 8, 10, 14))
        };

        Canvas reviewCanvas = new()
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0))
        };

        reviewCanvas.Children.Add(previewHost);
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

    public void Confirm() => Complete(bitmap);

    public void Dismiss() => Complete(null);

    public void CancelImmediately() => Complete(null);

    private void Complete(DesktopCaptureBitmap? result)
    {
        if (completed)
        {
            return;
        }

        completed = true;
        completion.TrySetResult(result);
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
}
