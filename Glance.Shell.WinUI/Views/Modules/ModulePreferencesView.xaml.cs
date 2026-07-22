using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class ModulePreferencesView :
    UserControl
{
    public ModulePreferencesView() => InitializeComponent();

    public ModulePreferencesViewModel ViewModel =>
        (ModulePreferencesViewModel)DataContext;

    public static Visibility WhenSettingsAvailable(bool hasSettings) =>
        hasSettings ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility WhenSettingsUnavailable(bool hasSettings) =>
        hasSettings ? Visibility.Collapsed : Visibility.Visible;

    private async void HandleDragItemsCompleted(
        ListViewBase sender,
        DragItemsCompletedEventArgs args) =>
        await ViewModel.SaveOrderAsync();
}
