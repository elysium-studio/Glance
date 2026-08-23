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

    private async void HandleAddProviderClick(object sender, RoutedEventArgs args)
    {
        if (XamlRoot is not null)
        {
            await ViewModel.ShowAddProviderDialogAsync(XamlRoot);
        }
    }

    private void HandleRemoveProviderClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { CommandParameter: AssistantTranscriptionProviderViewModel provider })
        {
            _ = ViewModel.RemoveProviderAsync(provider);
        }
    }
}
