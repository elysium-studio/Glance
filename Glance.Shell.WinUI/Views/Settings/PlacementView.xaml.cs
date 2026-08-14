using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class PlacementView :
    UserControl
{
    public PlacementView() => InitializeComponent();

    public PlacementViewModel ViewModel => (PlacementViewModel)DataContext;
}
