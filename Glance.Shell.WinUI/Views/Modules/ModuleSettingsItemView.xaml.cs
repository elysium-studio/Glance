using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class ModuleSettingsItemView :
    UserControl
{
    public ModuleSettingsItemView() => InitializeComponent();

    public ModuleSettingsItemViewModel ViewModel => (ModuleSettingsItemViewModel)DataContext;

    public Thickness GetToggleMargin(bool canExpand) => canExpand ? new() : new(0, 0, 28, 0);

    public Visibility GetUninstallVisibility(bool canUninstall) => canUninstall ? Visibility.Visible : Visibility.Collapsed;

    private async void HandleUninstallClicked(object sender,
        RoutedEventArgs args)
    {
        args.Handled = true;

        if (!ViewModel.CanUninstall || XamlRoot is null)
        {
            return;
        }

        UninstallModuleDialog dialog = new(ViewModel)
        {
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _ = await ViewModel.UninstallAsync();
        }
    }
}
