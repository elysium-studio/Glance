using Microsoft.UI.Xaml.Controls;
using Glance.Application.Abstractions;

namespace Glance.Assistant.WinUI;

public sealed partial class AssistantOverlayView :
    UserControl
{
    public AssistantOverlayView(IGlanceAssistantProvider provider)
    {
        Provider = provider;
        InitializeComponent();
    }

    public IGlanceAssistantProvider Provider { get; }
}
