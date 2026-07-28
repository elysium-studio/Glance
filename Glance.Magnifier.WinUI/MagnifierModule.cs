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
        services.AddSingleton<ModuleResourceTextLocalizer<MagnifierModule>>();
        services.AddSingleton<IMagnifierService, WindowsMagnifierService>();
        services.AddSingleton(provider => new MagnifierViewModel(provider.GetRequiredService<IMagnifierService>(), provider.GetRequiredService<ModuleResourceTextLocalizer<MagnifierModule>>()));
        services.AddSingleton<IGlanceComponent, MagnifierComponent>();
    }
}
