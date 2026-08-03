using System.ComponentModel;

namespace Glance.Application.Abstractions;

public interface IGlanceAssistantSemanticResolverService :
    INotifyPropertyChanged
{
    IReadOnlyList<IGlanceAssistantSemanticResolver> Resolvers { get; }

    IGlanceAssistantSemanticResolver? ActiveResolver { get; }

    Task SetActiveResolverAsync(string resolverId, CancellationToken cancellationToken = default);

    Task<GlanceAssistantCommandResult> TryExecuteAsync(string command, CancellationToken cancellationToken = default);
}
