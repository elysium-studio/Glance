using Elysium.UI.Controls.WinUI;

namespace Glance.Shell.WinUI;

public partial class DesktopIslandView : 
    DesktopIsland
{
    public DesktopIslandView() => InitializeComponent();

    public DesktopIslandViewModel ViewModel => (DesktopIslandViewModel)DataContext;
}
