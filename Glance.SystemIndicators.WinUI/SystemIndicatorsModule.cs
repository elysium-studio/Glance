using Glance.Application.Abstractions;
using Glance.SystemIndicators;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.SystemIndicators.WinUI;

public sealed class SystemIndicatorsModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<SystemIndicatorsModule>>();
        _ = services.AddSingleton<SystemIndicatorsViewModel>();
        _ = services.AddSingleton<ISystemIndicatorService, WindowsSystemIndicatorService>();
        _ = services.AddSingleton<SystemIndicatorsComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<SystemIndicatorsComponent>());
    }
}
