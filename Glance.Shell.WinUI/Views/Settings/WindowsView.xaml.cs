using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class WindowsView :
    UserControl
{
    public WindowsView() => InitializeComponent();

    public WindowsViewModel ViewModel => (WindowsViewModel)DataContext;
}
