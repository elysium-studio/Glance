using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.FocusSession.WinUI;

public sealed class FocusSessionModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ModuleResourceTextLocalizer<FocusSessionModule>>();
        services.AddSingleton<FocusSessionViewModel>();
        services.AddSingleton<IGlanceComponent, FocusSessionComponent>();
    }
}
