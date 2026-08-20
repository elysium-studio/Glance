namespace Glance.Application.Abstractions;

public interface IGlanceInspectorProviderRegistry
{
    IReadOnlyList<IGlanceInspectorProvider> GetProviders(GlanceContentContext context);
}
