using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class AssistantProviderView :
    UserControl
{
    public AssistantProviderView() => InitializeComponent();

    public AssistantProviderViewModel ViewModel => (AssistantProviderViewModel)DataContext;
}
