using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Power.WinUI;

public sealed class PowerModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<PowerViewModel>();
        services.AddSingleton<IGlanceComponent, PowerComponent>();
    }
}
