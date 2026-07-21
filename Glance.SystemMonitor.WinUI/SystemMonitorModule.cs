using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.SystemMonitor.WinUI;

public sealed class SystemMonitorModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ModuleResourceTextLocalizer<SystemMonitorModule>>();
        services.AddSingleton(provider => new SystemMonitorViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<SystemMonitorModule>>()));
        services.AddSingleton<IGlanceComponent, SystemMonitorComponent>();
    }
}
