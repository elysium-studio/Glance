using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Stopwatch.WinUI;

public sealed class StopwatchModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ModuleResourceTextLocalizer<StopwatchModule>>();
        services.AddSingleton<StopwatchViewModel>();
        services.AddSingleton<IGlanceComponent, StopwatchComponent>();
    }
}
