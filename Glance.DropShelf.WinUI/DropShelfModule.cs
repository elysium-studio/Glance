using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.DropShelf.WinUI;

public sealed class DropShelfModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddModuleOptions<DropShelfSettings>("DropShelf", "drop-shelf.settings.dat", DropShelfJsonContext.Default);
        _ = services.AddSingleton<ModuleResourceTextLocalizer<DropShelfModule>>();
        _ = services.AddSingleton<DropShelfTransferStore>();
        _ = services.AddSingleton(provider => new DropShelfViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<DropShelfModule>>(), provider.GetRequiredService<GlanceModuleOptions<DropShelfSettings>>().Current));
        _ = services.AddSingleton<DropShelfComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<DropShelfComponent>());
        _ = services.AddSingleton<IGlanceIntent>(provider => provider.GetRequiredService<DropShelfComponent>());
        _ = services.AddViewFor<ItemLimitSettingView, IGlanceModuleSettingViewModel, ItemLimitSettingViewModel>(ServiceLifetime.Transient, provider => new ItemLimitSettingView(), provider => new ItemLimitSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<DropShelfSettings>>().Current, provider.GetRequiredService<IWritableOptions<DropShelfSettings>>()));
    }
}
