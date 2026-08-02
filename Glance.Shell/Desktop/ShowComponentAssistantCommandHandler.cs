using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Shell;

public sealed class ShowComponentAssistantCommandHandler(IServiceProvider provider,
    ModulePreferenceService modulePreferences) :
    IGlanceAssistantCommandHandler
{
    public int Priority => -100;

    public Task<GlanceAssistantCommandResult> TryHandleAsync(string command, CancellationToken cancellationToken = default)
    {
        string normalizedCommand = command.Trim();

        if (!normalizedCommand.StartsWith("show ", StringComparison.OrdinalIgnoreCase) &&
            !normalizedCommand.StartsWith("open ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(GlanceAssistantCommandResult.NotHandled);
        }

        string requestedComponent = normalizedCommand[normalizedCommand.IndexOf(' ')..].Trim();
        IGlanceComponent? component = modulePreferences.GetActiveComponents()
            .OrderByDescending(candidate => candidate.DisplayName.Length)
            .FirstOrDefault(candidate => requestedComponent.Contains(candidate.DisplayName, StringComparison.OrdinalIgnoreCase) || requestedComponent.Contains(candidate.Id, StringComparison.OrdinalIgnoreCase));

        if (component is null)
        {
            return Task.FromResult(GlanceAssistantCommandResult.NotHandled);
        }

        DesktopIslandViewModel viewModel = provider.GetRequiredKeyedService<DesktopIslandViewModel>("DesktopIslandView");
        viewModel.ShowComponent(component.Id);
        return Task.FromResult(new GlanceAssistantCommandResult(true, $"Showing {component.DisplayName}"));
    }
}
