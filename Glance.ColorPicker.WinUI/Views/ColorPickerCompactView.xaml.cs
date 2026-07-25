using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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

    public FrameworkElement ConnectedAnimationElement => PaletteIcon;
}
