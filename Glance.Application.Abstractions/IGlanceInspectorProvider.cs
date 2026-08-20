namespace Glance.Application.Abstractions;

public interface IGlanceInspectorProvider
{
    GlanceInspectorProviderDescriptor Descriptor { get; }

    GlanceInspectorMatch Match(GlanceContentContext context);

    Task<GlanceInspectionResult> InspectAsync(GlanceContentContext context, CancellationToken cancellationToken = default);
}
