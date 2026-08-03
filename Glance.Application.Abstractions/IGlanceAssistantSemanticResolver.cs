namespace Glance.Application.Abstractions;

public interface IGlanceAssistantSemanticResolver
{
    string Id { get; }

    string DisplayName { get; }

    Task<GlanceAssistantActionResolution?> ResolveAsync(string command,
        IReadOnlyList<GlanceActionDescriptor> actions,
        CancellationToken cancellationToken = default);
}
