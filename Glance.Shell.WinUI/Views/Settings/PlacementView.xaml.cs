using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class PlacementView :
    UserControl
{
    public PlacementView() => InitializeComponent();

    public PlacementViewModel ViewModel => (PlacementViewModel)DataContext;

    public int SelectedPlacementIndex
    {
        get => ViewModel.Value switch
        {
            (int)GlancePlacement.Top => 0,
            (int)GlancePlacement.Bottom => 1,
            _ => 0
        };
        set => ViewModel.Value = value switch
        {
            1 => (int)GlancePlacement.Bottom,
            _ => (int)GlancePlacement.Top
        };
    }
}
