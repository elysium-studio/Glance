using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.SystemMonitor.WinUI;

public sealed class SystemMonitorModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<SystemMonitorViewModel>();
        services.AddSingleton<IGlanceComponent, SystemMonitorComponent>();
    }
}
