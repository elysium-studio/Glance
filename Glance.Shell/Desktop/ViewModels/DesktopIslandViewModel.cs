using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.Shell;

public partial class DesktopIslandViewModel :
    ObservableObject
{
    [ObservableProperty]
    private bool isOpen = true;
}
