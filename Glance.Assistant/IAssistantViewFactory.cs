using Glance.Application.Abstractions;

namespace Glance.Assistant;

public interface IAssistantViewFactory
{
    object CreateCompactIndicator(IGlanceAssistantProvider provider);

    object CreateExpandedIndicator(IGlanceAssistantProvider provider);

    object CreateOverlay(IGlanceAssistantProvider provider);
}
