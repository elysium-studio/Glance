using Glance.Application.Abstractions;
using Glance.Transcription.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Assistant.WinUI;

public sealed class AssistantModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddWindowsTranscription();
        _ = services
            .AddSingleton<IAssistantViewFactory, AssistantViewFactory>()
            .AddSingleton<IGlanceAssistantProvider, MicrosoftOfflineAssistantProvider>()
            .AddSingleton<IGlanceAssistantSemanticResolver, FoundryLocalSemanticResolver>();
    }
}
