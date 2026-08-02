namespace Glance.Application.Abstractions;

public interface IGlanceAssistantCommandHandler
{
    int Priority => 0;

    Task<GlanceAssistantCommandResult> TryHandleAsync(string command, CancellationToken cancellationToken = default);
}
