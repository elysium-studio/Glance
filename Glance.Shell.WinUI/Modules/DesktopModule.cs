using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Presentation.Abstractions;
using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Glance.Shell.WinUI;

public sealed class DesktopModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services
            .AddSingleton<IGlanceAttentionService, GlanceAttentionService>()
            .AddSingleton<GlanceAssistantCommandService>()
            .AddSingleton<IGlanceAssistantCommandService>(provider => provider.GetRequiredService<GlanceAssistantCommandService>())
            .AddSingleton<GlanceAssistantService>()
            .AddSingleton<IGlanceAssistantService>(provider => provider.GetRequiredService<GlanceAssistantService>())
            .AddSingleton<IGlanceAssistantCommandHandler, ShowComponentAssistantCommandHandler>()
            .AddSingleton<ModulePreferenceService>()
            .AddSingleton<GlanceActionService>()
            .AddSingleton<IGlanceActionService>(provider => provider.GetRequiredService<GlanceActionService>())
            .AddSingleton<GlanceIntentService>()
            .AddSingleton<IGlanceIntentService>(provider => provider.GetRequiredService<GlanceIntentService>())
            .AddViewFor(ServiceLifetime.Singleton,
                provider => new DesktopIslandView(),
                provider => new DesktopIslandViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<ModulePreferenceService>(), provider.GetRequiredService<IGlanceAttentionService>(), provider.GetRequiredService<IGlanceAssistantService>(), provider.GetRequiredService<IGlanceActionService>(), provider.GetRequiredService<IGlanceIntentService>(), provider.GetRequiredService<INavigator>(), provider.GetRequiredService<ILogger<DesktopIslandViewModel>>(), provider.GetRequiredService<GlanceSettings>(), provider.GetRequiredService<IWritableOptions<GlanceSettings>>()));
    }
}
