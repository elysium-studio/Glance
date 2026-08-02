using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed class GlanceAssistantCommandService(IEnumerable<IGlanceAssistantCommandHandler> handlers) :
    IGlanceAssistantCommandService
{
    private readonly List<IGlanceAssistantCommandHandler> handlers = [.. handlers];
    private readonly object synchronization = new();

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
