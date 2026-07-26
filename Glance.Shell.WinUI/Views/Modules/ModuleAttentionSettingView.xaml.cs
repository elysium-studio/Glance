using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class ModuleAttentionSettingView :
    UserControl
{
    public ModuleAttentionSettingView() => InitializeComponent();

    public ModuleAttentionSettingViewModel ViewModel =>
        (ModuleAttentionSettingViewModel)DataContext;
}
