namespace Glance.Application.Abstractions;

public interface IGlanceAssistantCommandService
{
    Task<GlanceAssistantCommandResult> ExecuteAsync(string command, CancellationToken cancellationToken = default);
}
