using Glance.Application.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Glance.Shell.WinUI;

public sealed partial class ModuleSettingsItemView :
    UserControl
{
    private readonly ITextLocalizer? localizer;

    public ModuleSettingsItemView() => InitializeComponent();

    public ModuleSettingsItemView(ITextLocalizer localizer)
    {
        this.localizer = localizer;
        InitializeComponent();
    }

    public ModuleSettingsItemViewModel ViewModel => (ModuleSettingsItemViewModel)DataContext;

    public Thickness GetToggleMargin(bool canExpand) => canExpand ? new() : new(0, 0, 28, 0);

    public Visibility GetUninstallVisibility(bool canUninstall) => canUninstall ? Visibility.Visible : Visibility.Collapsed;

    private async void HandleUninstallClicked(object sender,
        RoutedEventArgs args)
    {
        if (!ViewModel.CanUninstall || localizer is null || XamlRoot is null)
        {
            return;
        }

        UninstallModuleDialog dialog = new(ViewModel.DisplayName, localizer)
        {
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _ = await ViewModel.UninstallAsync();
        }
    }
}
