using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Power.WinUI;

public sealed class PowerModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ModuleResourceTextLocalizer<PowerModule>>();
        services.AddSingleton(provider => new PowerViewModel(
            provider.GetRequiredService<ModuleResourceTextLocalizer<PowerModule>>()));
        services.AddSingleton<IGlanceComponent, PowerComponent>();
    }
}
