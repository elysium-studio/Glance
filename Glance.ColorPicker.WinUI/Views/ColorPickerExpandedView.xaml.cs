using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.ComponentModel;
using System.Collections.ObjectModel;

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
            new ColorFormatItem("HEX", viewModel.Hex, viewModel.CopyHex, viewModel.Pick),
            new ColorFormatItem("RGB", viewModel.Rgb, viewModel.CopyRgb, viewModel.Pick),
            new ColorFormatItem("HSL", viewModel.Hsl, viewModel.CopyHsl, viewModel.Pick)
        ];
        viewModel.PropertyChanged += HandleViewModelPropertyChanged;
        InitializeComponent();
    }

    public ColorPickerViewModel ViewModel { get; }

    public ObservableCollection<ColorFormatItem> Formats { get; }

    public FrameworkElement ConnectedAnimationElement => ColorSwatch;

    public string Title => localizer.GetText("ModuleDisplayName");

    private string ToUpper(string value) => value.ToUpperInvariant();

    private SolidColorBrush ToBrush(ColorValue color) =>
        new(Windows.UI.Color.FromArgb(255, color.Red, color.Green, color.Blue));

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
}
