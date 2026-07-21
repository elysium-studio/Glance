using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class StartWithWindowsView :
    UserControl
{
    public StartWithWindowsView() => InitializeComponent();

    public StartWithWindowsViewModel ViewModel => (StartWithWindowsViewModel)DataContext;
}
