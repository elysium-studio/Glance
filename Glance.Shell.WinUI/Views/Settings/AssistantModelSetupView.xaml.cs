using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class AssistantModelSetupView :
    UserControl
{
    public AssistantModelSetupView() => InitializeComponent();

    public AssistantModelSetupViewModel ViewModel => (AssistantModelSetupViewModel)DataContext;

    private void HandleInstallClick(object sender, RoutedEventArgs args) => _ = ViewModel.InstallAsync();

    private void HandleCancelClick(object sender, RoutedEventArgs args) => ViewModel.Cancel();

    private void HandleRemoveClick(object sender, RoutedEventArgs args) => _ = ViewModel.RemoveAsync();
}
