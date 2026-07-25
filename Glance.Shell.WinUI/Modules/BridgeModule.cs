using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Shell.WinUI;

public sealed class BridgeModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<GlanceBridgeRouter>();
        services.AddHostedService<GlanceBridgeServer>();
    }
}
