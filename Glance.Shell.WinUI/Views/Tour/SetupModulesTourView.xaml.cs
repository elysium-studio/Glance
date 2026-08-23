using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class SetupModulesTourView :
    UserControl
{
    public SetupModulesTourView() => InitializeComponent();

    public SetupTourViewModel ViewModel => (SetupTourViewModel)DataContext;

    private async void HandleInstallClicked(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { DataContext: SetupTourModuleViewModel module })
        {
            await module.InstallAsync();
        }
    }

    private async void HandleRemoveClicked(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { DataContext: SetupTourModuleViewModel module })
        {
            await module.RemoveAsync();
        }
    }
}
