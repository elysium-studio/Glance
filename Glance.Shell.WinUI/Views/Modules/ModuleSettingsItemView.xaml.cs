using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class ModuleSettingsItemView :
    UserControl
{
    public ModuleSettingsItemView() => InitializeComponent();

    public ModuleSettingsItemViewModel ViewModel => (ModuleSettingsItemViewModel)DataContext;

    public Thickness GetToggleMargin(bool canExpand) => canExpand ? new() : new(0, 0, 28, 0);
}
