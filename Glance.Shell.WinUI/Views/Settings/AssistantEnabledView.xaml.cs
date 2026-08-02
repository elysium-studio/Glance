using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class AssistantEnabledView :
    UserControl
{
    public AssistantEnabledView() => InitializeComponent();

    public AssistantEnabledViewModel ViewModel => (AssistantEnabledViewModel)DataContext;
}
