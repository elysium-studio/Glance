using Elysium.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Shell.WinUI;

public sealed class BridgeModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<GlanceBridgeRouter>();
        _ = services.AddHostedService<GlanceBridgeServer>();
    }
}
