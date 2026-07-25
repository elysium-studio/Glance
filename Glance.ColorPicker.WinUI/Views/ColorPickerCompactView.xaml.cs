using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Glance.ColorPicker.WinUI;

public sealed partial class ColorPickerCompactView :
    UserControl
{
    public ColorPickerCompactView(ColorPickerViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public ColorPickerViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => ColorSwatch;

    private SolidColorBrush ToBrush(ColorValue color) =>
        new(Windows.UI.Color.FromArgb(255, color.Red, color.Green, color.Blue));
}
