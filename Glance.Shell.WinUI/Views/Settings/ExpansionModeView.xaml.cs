using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class ExpansionModeView :
    UserControl
{
    public ExpansionModeView() => InitializeComponent();

    public ExpansionModeViewModel ViewModel => (ExpansionModeViewModel)DataContext;
}
