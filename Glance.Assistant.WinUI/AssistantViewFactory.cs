using Glance.Application.Abstractions;
using Glance.Assistant;

namespace Glance.Assistant.WinUI;

public sealed class AssistantViewFactory(IGlanceAssistantService assistant) :
    IAssistantViewFactory
{
    public object CreateCompactIndicator(IGlanceAssistantProvider provider) => new AssistantIndicatorView(provider, assistant);

    public object CreateExpandedIndicator(IGlanceAssistantProvider provider) => new AssistantIndicatorView(provider, assistant);

    public object CreateOverlay(IGlanceAssistantProvider provider) => new AssistantOverlayView(provider);
}
