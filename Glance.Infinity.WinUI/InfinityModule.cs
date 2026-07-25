using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Infinity.WinUI;

public sealed class InfinityModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ModuleResourceTextLocalizer<InfinityModule>>();
        services.AddSingleton<InfinityViewModel>();
        services.AddSingleton<IGlanceComponent, InfinityComponent>();
        services.AddSingleton<IGlanceApplicationMessageHandler, InfinityMessageHandler>();
    }
}
