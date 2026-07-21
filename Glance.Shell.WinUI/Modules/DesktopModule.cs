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
            .AddSingleton<ModulePreferenceService>()
            .AddViewFor(
                ServiceLifetime.Singleton,
                provider => new DesktopIslandView(),
                provider => new DesktopIslandViewModel(
                    provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<ModulePreferenceService>(),
                    provider.GetRequiredService<IGlanceAttentionService>(),
                    provider.GetRequiredService<INavigator>(),
                    provider.GetRequiredService<ILogger<DesktopIslandViewModel>>(),
                    provider.GetRequiredService<GlanceSettings>()));
    }
}
