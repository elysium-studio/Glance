using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed class GlanceAssistantCommandService :
    IGlanceAssistantCommandService
{
    private readonly List<IGlanceAssistantCommandHandler> handlers;
    private readonly IGlanceAssistantSemanticResolverService? semanticResolvers;
    private readonly object synchronization = new();

    public GlanceAssistantCommandService(IEnumerable<IGlanceAssistantCommandHandler> handlers,
        IGlanceAssistantSemanticResolverService semanticResolvers)
    {
        this.handlers = [.. handlers];
        this.semanticResolvers = semanticResolvers;
    }

    public GlanceAssistantCommandService(IEnumerable<IGlanceAssistantCommandHandler> handlers)
    {
        this.handlers = [.. handlers];
    }

    public void Register(IEnumerable<IGlanceAssistantCommandHandler> registrations)
    {
        lock (synchronization)
        {
            foreach (IGlanceAssistantCommandHandler handler in registrations)
            {
                if (!handlers.Contains(handler))
                {
                    handlers.Add(handler);
                }
            }
        }
    }

    public async Task<GlanceAssistantCommandResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        if (semanticResolvers is not null)
        {
            GlanceAssistantCommandResult semanticResult = await semanticResolvers.TryExecuteAsync(command, cancellationToken);

            if (semanticResult.Handled || !string.IsNullOrWhiteSpace(semanticResult.Response))
            {
                return semanticResult;
            }
        }

        IGlanceAssistantCommandHandler[] snapshot;

        lock (synchronization)
        {
            snapshot = [.. handlers.OrderByDescending(handler => handler.Priority)];
        }

        foreach (IGlanceAssistantCommandHandler handler in snapshot)
        {
            GlanceAssistantCommandResult result = await handler.TryHandleAsync(command, cancellationToken);

            if (result.Handled)
            {
                return result;
            }
        }

        return GlanceAssistantCommandResult.NotHandled;
    }
}
