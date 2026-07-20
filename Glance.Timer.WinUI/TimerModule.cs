using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Timer.WinUI;

public sealed class TimerModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ModuleResourceTextLocalizer<TimerModule>>();
        services.AddSingleton<TimerViewModel>();
        services.AddSingleton<IGlanceComponent, TimerComponent>();
    }
}
