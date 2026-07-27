using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class ModulePreferencesView :
    UserControl
{
    private readonly ModuleSettingsNavigationService navigation;

    public ModulePreferencesView(ModuleSettingsNavigationService navigation)
    {
        this.navigation = navigation;
        InitializeComponent();
    }

    public ModulePreferencesViewModel ViewModel =>
        (ModulePreferencesViewModel)DataContext;

    private async void HandleDragItemsCompleted(ListViewBase sender,
        DragItemsCompletedEventArgs args) =>
        await ViewModel.SaveOrderAsync();

    private void HandleModuleClick(object sender,
        RoutedEventArgs args)
    {
        if (sender is SettingsCard { DataContext: ModuleSettingsItemViewModel module } &&
            module.CanExpand)
        {
            navigation.NavigateTo(module);
        }
    }
}
