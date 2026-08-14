using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class DisplayLocationView :
    UserControl
{
    public DisplayLocationView() => InitializeComponent();

    public DisplayLocationViewModel ViewModel => (DisplayLocationViewModel)DataContext;
}
