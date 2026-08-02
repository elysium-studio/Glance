using Microsoft.UI.Xaml.Controls;

namespace Glance.Assistant.WinUI;

public sealed partial class AssistantOverlayView :
    UserControl
{
    public AssistantOverlayView(MicrosoftOfflineAssistantProvider provider)
    {
        Provider = provider;
        InitializeComponent();
    }

    public MicrosoftOfflineAssistantProvider Provider { get; }
}
