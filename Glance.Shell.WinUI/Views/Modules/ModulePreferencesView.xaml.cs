using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class ModulePreferencesView :
    UserControl
{
    public ModulePreferencesView() => InitializeComponent();

    public ModulePreferencesViewModel ViewModel =>
        (ModulePreferencesViewModel)DataContext;

    private async void HandleDragItemsCompleted(
        ListViewBase sender,
        DragItemsCompletedEventArgs args) =>
        await ViewModel.SaveOrderAsync();
}
