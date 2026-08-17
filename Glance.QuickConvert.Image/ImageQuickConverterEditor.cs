using Glance.Application.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Glance.QuickConvert.Image;

internal sealed class ImageQuickConverterEditor :
    IGlanceQuickConverterEditor
{
    private readonly ComboBox formatPicker;
    private readonly NumberBox heightBox;
    private readonly ToggleButton aspectRatioButton;
    private readonly Grid dimensions;
    private readonly ModuleResourceTextLocalizer<ImageQuickConverterModule> localizer;
    private readonly NumberBox percentageBox;
    private readonly Slider qualitySlider;
    private readonly ComboBox scalePicker;
    private readonly NumberBox widthBox;
    private bool isUpdatingDimensions;
    private double lockedAspectRatio = 16d / 9;

    public ImageQuickConverterEditor(ModuleResourceTextLocalizer<ImageQuickConverterModule> localizer)
    {
        this.localizer = localizer;
        formatPicker = new ComboBox
        {
            Header = localizer.GetText("OutputFormat"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[] { "PNG", "JPEG", "BMP", "TIFF", "GIF" },
            SelectedIndex = 0
        };
        scalePicker = new ComboBox
        {
            Header = localizer.GetText("ImageSize"),
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
            Margin = new Thickness(0),
            Visibility = Visibility.Collapsed
        };
        widthBox = new NumberBox
        {
            Header = localizer.GetText("MaximumWidth"),
            Minimum = 1,
            Maximum = 32768,
            Value = 1920,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Margin = new Thickness(0)
        };
        heightBox = new NumberBox
        {
            Header = localizer.GetText("MaximumHeight"),
            Minimum = 1,
            Maximum = 32768,
            Value = 1080,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Margin = new Thickness(0)
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
        qualitySlider = new Slider
        {
            Header = localizer.GetText("ImageQuality"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Minimum = 10,
            Maximum = 100,
            Value = 90
        };
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
        StackPanel content = new() { Spacing = 12 };
        content.Children.Add(formatPicker);
        content.Children.Add(scalePicker);
        content.Children.Add(percentageBox);
        content.Children.Add(dimensions);
        content.Children.Add(qualitySlider);
        Content = content;
        scalePicker.SelectionChanged += HandleScaleChanged;
        formatPicker.SelectionChanged += HandleFormatChanged;
        aspectRatioButton.Checked += HandleAspectRatioChanged;
        aspectRatioButton.Unchecked += HandleAspectRatioChanged;
        widthBox.ValueChanged += HandleWidthChanged;
        heightBox.ValueChanged += HandleHeightChanged;
        HandleFormatChanged(formatPicker, null!);
    }

    public object Content { get; }

    public bool TryCreateOptions(out object? options,
        out string? errorMessage)
    {
        ImageScaleMode scaleMode = (ImageScaleMode)Math.Max(0, scalePicker.SelectedIndex);

        if ((scaleMode == ImageScaleMode.Percentage && double.IsNaN(percentageBox.Value)) ||
            (scaleMode == ImageScaleMode.FitWithin && (double.IsNaN(widthBox.Value) || double.IsNaN(heightBox.Value))))
        {
            options = null;
            errorMessage = localizer.GetText("ValidSizeRequired");
            return false;
        }

        string format = formatPicker.SelectedItem?.ToString()?.ToLowerInvariant() ?? "png";
        options = new ImageConversionOptions(format == "jpeg" ? "jpg" : format,
            scaleMode,
            percentageBox.Value,
            (uint)widthBox.Value,
            (uint)heightBox.Value,
            qualitySlider.Value / 100);
        errorMessage = null;
        return true;
    }

    private void HandleScaleChanged(object sender,
        SelectionChangedEventArgs args)
    {
        percentageBox.Visibility = scalePicker.SelectedIndex == (int)ImageScaleMode.Percentage
            ? Visibility.Visible : Visibility.Collapsed;
        dimensions.Visibility = scalePicker.SelectedIndex == (int)ImageScaleMode.FitWithin
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HandleFormatChanged(object sender,
        SelectionChangedEventArgs args) => qualitySlider.Visibility = formatPicker.SelectedIndex == 1
            ? Visibility.Visible : Visibility.Collapsed;

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
