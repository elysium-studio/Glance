using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Glance.ColorPicker.WinUI;

public sealed partial class ColorPickerExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<ColorPickerModule> localizer;

    public ColorPickerExpandedView(
        ColorPickerViewModel viewModel,
        ModuleResourceTextLocalizer<ColorPickerModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        Formats =
        [
            new ColorFormatItem("HEX", viewModel.Hex, viewModel.CopyHex),
            new ColorFormatItem("RGB", viewModel.Rgb, viewModel.CopyRgb),
            new ColorFormatItem("HSL", viewModel.Hsl, viewModel.CopyHsl)
        ];
        RecentColors = [];
        viewModel.PropertyChanged += HandleViewModelPropertyChanged;
        viewModel.RecentColors.CollectionChanged += HandleRecentColorsChanged;
        RefreshRecentColors();
        InitializeComponent();
    }

    public ColorPickerViewModel ViewModel { get; }

    public ObservableCollection<ColorFormatItem> Formats { get; }

    public ObservableCollection<ColorSwatchItem> RecentColors { get; }

    public FrameworkElement ConnectedAnimationElement => ColorSwatch;

    public string Title => localizer.GetText("ModuleDisplayName");

    private string ToUpper(string value) => value.ToUpperInvariant();

    private SolidColorBrush ToBrush(ColorValue color) =>
        new(Windows.UI.Color.FromArgb(255, color.Red, color.Green, color.Blue));

    private Visibility WhenIdle(bool isPicking) =>
        isPicking ? Visibility.Collapsed : Visibility.Visible;

    private Visibility WhenPicking(bool isPicking) =>
        isPicking ? Visibility.Visible : Visibility.Collapsed;

    private void HandleRecentColorsChanged(
        object? sender,
        NotifyCollectionChangedEventArgs args) =>
        RefreshRecentColors();

    private void HandleViewModelPropertyChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(ColorPickerViewModel.CurrentColor))
        {
            return;
        }

        Formats[0].Update(ViewModel.Hex);
        Formats[1].Update(ViewModel.Rgb);
        Formats[2].Update(ViewModel.Hsl);
    }

    private void RefreshRecentColors()
    {
        RecentColors.Clear();

        foreach (ColorValue color in ViewModel.RecentColors)
        {
            RecentColors.Add(new ColorSwatchItem(color, ViewModel.SelectRecent));
        }
    }
}
