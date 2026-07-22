using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class AutoHideView :
    UserControl
{
    public AutoHideView() => InitializeComponent();

    public AutoHideViewModel ViewModel => (AutoHideViewModel)DataContext;
}
