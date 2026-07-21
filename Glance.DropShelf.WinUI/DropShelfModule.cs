using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.DropShelf.WinUI;

public sealed class DropShelfModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ModuleResourceTextLocalizer<DropShelfModule>>();
        services.AddSingleton<DropShelfTransferStore>();
        services.AddSingleton(provider => new DropShelfViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<DropShelfModule>>()));
        services.AddSingleton<IGlanceComponent, DropShelfComponent>();
    }
}
