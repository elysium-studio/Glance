using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class ModulesView :
    UserControl
{
    public ModulesView() => InitializeComponent();

    public ModulesViewModel ViewModel => (ModulesViewModel)DataContext;
}
