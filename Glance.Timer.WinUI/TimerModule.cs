using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Timer.WinUI;

public sealed class TimerModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<TimerViewModel>();
        services.AddSingleton<IGlanceComponent, TimerComponent>();
    }
}
