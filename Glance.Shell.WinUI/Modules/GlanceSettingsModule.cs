using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Shell.WinUI;

public sealed class GlanceSettingsModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services.AddViewFor<PlacementView, IGlanceViewModel, PlacementViewModel>(
            ServiceLifetime.Transient,
            provider => new PlacementView(),
            provider => new PlacementViewModel(
                provider,
                provider.GetRequiredService<IServiceFactory>(),
                provider.GetRequiredService<IMessenger>(),
                provider.GetRequiredService<IDisposer>(),
                provider.GetRequiredService<IDispatcher>(),
                provider.GetRequiredService<GlanceSettings>(),
                provider.GetRequiredService<IWritableOptions<GlanceSettings>>(),
                config => (int)config.Placement,
                (config, placement) => config.Placement = (GlancePlacement)placement));
    }
}
