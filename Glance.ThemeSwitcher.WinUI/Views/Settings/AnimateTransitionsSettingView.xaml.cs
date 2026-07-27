using Microsoft.UI.Xaml.Controls;

namespace Glance.ThemeSwitcher.WinUI;

public sealed partial class AnimateTransitionsSettingView :
    UserControl
{
    public AnimateTransitionsSettingView() => InitializeComponent();

    public AnimateTransitionsSettingViewModel ViewModel => (AnimateTransitionsSettingViewModel)DataContext;
}
