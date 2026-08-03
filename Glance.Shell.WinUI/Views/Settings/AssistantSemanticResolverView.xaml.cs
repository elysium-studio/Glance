using Glance.Shell;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class AssistantSemanticResolverView :
    UserControl
{
    public AssistantSemanticResolverView() => InitializeComponent();

    public AssistantSemanticResolverViewModel ViewModel => (AssistantSemanticResolverViewModel)DataContext;
}
