using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.KeepAwake.WinUI;

public sealed class KeepAwakeModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ModuleResourceTextLocalizer<KeepAwakeModule>>();
        services.AddSingleton<IKeepAwakeService, WindowsKeepAwakeService>();
        services.AddSingleton(provider => new KeepAwakeViewModel(provider.GetRequiredService<IKeepAwakeService>(), provider.GetRequiredService<ModuleResourceTextLocalizer<KeepAwakeModule>>(), provider.GetRequiredService<IDispatcher>()));
        services.AddSingleton<IGlanceComponent, KeepAwakeComponent>();
    }
}
