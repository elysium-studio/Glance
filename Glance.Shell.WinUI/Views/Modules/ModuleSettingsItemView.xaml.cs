using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class ModuleSettingsItemView :
    UserControl
{
    public ModuleSettingsItemView() => InitializeComponent();

    public ModuleSettingsItemViewModel ViewModel => (ModuleSettingsItemViewModel)DataContext;
}
