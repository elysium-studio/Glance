using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Assistant.WinUI;

public sealed class AssistantModule :
    IGlanceModule
{
    public void Register(IServiceCollection services) =>
        services.AddSingleton<IGlanceAssistantProvider, MicrosoftOfflineAssistantProvider>();
}
