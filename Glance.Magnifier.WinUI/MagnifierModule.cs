using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Magnifier.WinUI;

public sealed class MagnifierModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<MagnifierModule>>();
        _ = services.AddSingleton<IMagnifierService, WindowsMagnifierService>();
        _ = services.AddSingleton(provider => new MagnifierViewModel(provider.GetRequiredService<IMagnifierService>(), provider.GetRequiredService<ModuleResourceTextLocalizer<MagnifierModule>>()));
        _ = services.AddSingleton<MagnifierComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<MagnifierComponent>());
        _ = services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<MagnifierComponent>());
    }
}
