using Glance.Application.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Glance.QuickConvert.Video;

internal sealed class VideoQuickConverterEditor :
    IGlanceQuickConverterEditor
{
    private readonly ComboBox formatPicker;
    private readonly NumberBox heightBox;
    private readonly ToggleButton aspectRatioButton;
    private readonly Grid dimensions;
    private readonly ModuleResourceTextLocalizer<VideoQuickConverterModule> localizer;
    private readonly NumberBox percentageBox;
    private readonly ComboBox qualityPicker;
    private readonly ComboBox scalePicker;
    private readonly StackPanel sizing;
    private readonly NumberBox widthBox;
    private bool isUpdatingDimensions;
    private double lockedAspectRatio = 16d / 9;

    public VideoQuickConverterEditor(ModuleResourceTextLocalizer<VideoQuickConverterModule> localizer)
    {
        this.localizer = localizer;
        formatPicker = new ComboBox
        {
            Header = localizer.GetText("OutputFormat"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[] { "MP4", "WebM", "MKV", "MOV", "AVI", "GIF", "MP3", "M4A", "WAV", "FLAC", "OGG" },
            SelectedIndex = 0
        };
        scalePicker = new ComboBox
        {
            Header = localizer.GetText("VideoSize"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[]
            {
                localizer.GetText("OriginalSize"),
                localizer.GetText("PercentageSize"),
                localizer.GetText("FitWithinSize")
            },
            SelectedIndex = 0
        };
        percentageBox = new NumberBox
        {
            Header = localizer.GetText("ScalePercentage"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Minimum = 1,
            Maximum = 400,
            Value = 100,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Visibility = Visibility.Collapsed
        };
        widthBox = new NumberBox
        {
            Header = localizer.GetText("MaximumWidth"),
            Minimum = 2,
            Maximum = 32768,
            Value = 1920,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
        };
        heightBox = new NumberBox
        {
            Header = localizer.GetText("MaximumHeight"),
            Minimum = 2,
            Maximum = 32768,
            Value = 1080,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
        };
        aspectRatioButton = new ToggleButton
        {
            Width = 36,
            Height = 32,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Bottom,
            IsChecked = true,
            Content = CreateAspectRatioIcon(true)
        };
        ToolTipService.SetToolTip(aspectRatioButton, localizer.GetText("LockAspectRatio"));
        dimensions = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ColumnSpacing = 8,
            Visibility = Visibility.Collapsed
        };
        dimensions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dimensions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        dimensions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(widthBox, 0);
        Grid.SetColumn(aspectRatioButton, 1);
        Grid.SetColumn(heightBox, 2);
        dimensions.Children.Add(widthBox);
        dimensions.Children.Add(aspectRatioButton);
        dimensions.Children.Add(heightBox);
        sizing = new StackPanel { Spacing = 12 };
        sizing.Children.Add(scalePicker);
        sizing.Children.Add(percentageBox);
        sizing.Children.Add(dimensions);
        qualityPicker = new ComboBox
        {
            Header = localizer.GetText("VideoQuality"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[]
            {
                localizer.GetText("SmallerFile"),
                localizer.GetText("BalancedQuality"),
                localizer.GetText("HighQuality")
            },
            SelectedIndex = (int)VideoConversionQuality.Balanced
        };
        StackPanel content = new() { Spacing = 12 };
        content.Children.Add(formatPicker);
        content.Children.Add(sizing);
        content.Children.Add(qualityPicker);
        Content = content;
        scalePicker.SelectionChanged += HandleScaleChanged;
        formatPicker.SelectionChanged += HandleFormatChanged;
        aspectRatioButton.Checked += HandleAspectRatioChanged;
        aspectRatioButton.Unchecked += HandleAspectRatioChanged;
        widthBox.ValueChanged += HandleWidthChanged;
        heightBox.ValueChanged += HandleHeightChanged;
    }

    public object Content { get; }

    public bool TryCreateOptions(out object? options,
        out string? errorMessage)
    {
        VideoScaleMode scaleMode = (VideoScaleMode)Math.Max(0, scalePicker.SelectedIndex);

        if ((scaleMode == VideoScaleMode.Percentage && double.IsNaN(percentageBox.Value)) ||
            (scaleMode == VideoScaleMode.FitWithin && (double.IsNaN(widthBox.Value) || double.IsNaN(heightBox.Value))))
        {
            options = null;
            errorMessage = localizer.GetText("ValidVideoSizeRequired");
            return false;
        }

        options = new VideoConversionOptions(formatPicker.SelectedItem?.ToString()?.ToLowerInvariant() ?? "mp4",
            scaleMode,
            percentageBox.Value,
            (uint)widthBox.Value,
            (uint)heightBox.Value,
            (VideoConversionQuality)Math.Max(0, qualityPicker.SelectedIndex));
        errorMessage = null;
        return true;
    }

    private void HandleScaleChanged(object sender,
        SelectionChangedEventArgs args)
    {
        percentageBox.Visibility = scalePicker.SelectedIndex == (int)VideoScaleMode.Percentage
            ? Visibility.Visible : Visibility.Collapsed;
        dimensions.Visibility = scalePicker.SelectedIndex == (int)VideoScaleMode.FitWithin
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HandleFormatChanged(object sender,
        SelectionChangedEventArgs args)
    {
        string format = formatPicker.SelectedItem?.ToString() ?? string.Empty;
        sizing.Visibility = VideoFfmpegArguments.IsAudioOnly(format) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void HandleAspectRatioChanged(object sender,
        RoutedEventArgs args)
    {
        bool isLocked = aspectRatioButton.IsChecked == true;
        aspectRatioButton.Content = CreateAspectRatioIcon(isLocked);
        ToolTipService.SetToolTip(aspectRatioButton, localizer.GetText(isLocked ? "UnlockAspectRatio" : "LockAspectRatio"));

        if (isLocked && IsValidDimension(widthBox.Value) && IsValidDimension(heightBox.Value))
        {
            lockedAspectRatio = widthBox.Value / heightBox.Value;
        }
    }

    private void HandleWidthChanged(NumberBox sender,
        NumberBoxValueChangedEventArgs args)
    {
        if (isUpdatingDimensions || aspectRatioButton.IsChecked != true || !IsValidDimension(args.NewValue))
        {
            return;
        }

        isUpdatingDimensions = true;
        heightBox.Value = Math.Clamp(Math.Round(args.NewValue / lockedAspectRatio), heightBox.Minimum, heightBox.Maximum);
        isUpdatingDimensions = false;
    }

    private void HandleHeightChanged(NumberBox sender,
        NumberBoxValueChangedEventArgs args)
    {
        if (isUpdatingDimensions || aspectRatioButton.IsChecked != true || !IsValidDimension(args.NewValue))
        {
            return;
        }

        isUpdatingDimensions = true;
        widthBox.Value = Math.Clamp(Math.Round(args.NewValue * lockedAspectRatio), widthBox.Minimum, widthBox.Maximum);
        isUpdatingDimensions = false;
    }

    private static FontIcon CreateAspectRatioIcon(bool isLocked) => new()
    {
        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
        FontSize = 14,
        Glyph = isLocked ? "\uE72E" : "\uE785"
    };

    private static bool IsValidDimension(double value) => !double.IsNaN(value) && value > 0;
}
