using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Infinity.WinUI;

public sealed class InfinityModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<InfinityModule>>();
        _ = services.AddSingleton<InfinityBridgeClient>();
        _ = services.AddSingleton<IInfinityPageTitleUpdater>(provider => provider.GetRequiredService<InfinityBridgeClient>());
        _ = services.AddSingleton<InfinityViewModel>();
        _ = services.AddSingleton<IGlanceComponent, InfinityComponent>();
        _ = services.AddSingleton<IGlanceApplicationMessageHandler, InfinityMessageHandler>();
    }
}
