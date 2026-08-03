using Glance.Application.Abstractions;
using Glance.Assistant;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Assistant.WinUI;

public sealed class AssistantModule :
    IGlanceModule
{
    public void Register(IServiceCollection services) => services
        .AddSingleton<IAssistantViewFactory, AssistantViewFactory>()
        .AddSingleton<IGlanceAssistantProvider, MicrosoftOfflineAssistantProvider>()
        .AddSingleton<IGlanceAssistantSemanticResolver, FoundryLocalSemanticResolver>();
}
