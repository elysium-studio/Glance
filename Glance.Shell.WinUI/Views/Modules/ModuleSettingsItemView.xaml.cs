using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Glance.Shell.WinUI;

public sealed partial class ModuleSettingsItemView :
    UserControl
{
    public ModuleSettingsItemView() => InitializeComponent();

    public ModuleSettingsItemViewModel ViewModel => (ModuleSettingsItemViewModel)DataContext;

    public Thickness GetToggleMargin(bool canExpand) => canExpand ? new() : new(0, 0, 28, 0);

    public Visibility GetUninstallVisibility(bool canUninstall) => canUninstall ? Visibility.Visible : Visibility.Collapsed;

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        ActualThemeChanged += HandleActualThemeChanged;
        ApplyHeaderIcon();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args) =>
        ActualThemeChanged -= HandleActualThemeChanged;

    private void HandleActualThemeChanged(FrameworkElement sender, object args) => ApplyHeaderIcon();

    private void ApplyHeaderIcon()
    {
        if (ViewModel.CreateIcon(ActualTheme == ElementTheme.Light) is IconElement icon)
        {
            SettingsCard.HeaderIcon = icon;
        }
    }

    private async void HandleUninstallClicked(object sender,
        RoutedEventArgs args)
    {
        if (!ViewModel.CanUninstall || XamlRoot is null)
        {
            return;
        }

        UninstallModuleDialog dialog = new(ViewModel.DisplayName)
        {
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _ = await ViewModel.UninstallAsync();
        }
    }
}
