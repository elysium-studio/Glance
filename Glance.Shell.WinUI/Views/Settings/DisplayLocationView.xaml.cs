using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class DisplayLocationView :
    UserControl
{
    public DisplayLocationView() => InitializeComponent();

    public DisplayLocationViewModel ViewModel => (DisplayLocationViewModel)DataContext;

    public int SelectedDisplayLocationIndex
    {
        get => ViewModel.Value == (int)GlanceDisplayLocation.Taskbar ? 1 : 0;
        set => ViewModel.Value = value == 1
            ? (int)GlanceDisplayLocation.Taskbar
            : (int)GlanceDisplayLocation.DesktopEdge;
    }
}
