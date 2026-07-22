using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.DropShelf.WinUI;

public sealed class DropShelfModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddModuleOptions<DropShelfSettings>("DropShelf", "drop-shelf.settings.dat", DropShelfJsonContext.Default);
        services.AddSingleton<ModuleResourceTextLocalizer<DropShelfModule>>();
        services.AddSingleton<DropShelfTransferStore>();
        services.AddSingleton(provider => new DropShelfViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<DropShelfModule>>(), provider.GetRequiredService<GlanceModuleOptions<DropShelfSettings>>().Current));
        services.AddSingleton<IGlanceComponent, DropShelfComponent>();
        services.AddViewFor<ItemLimitSettingView, IGlanceModuleSettingViewModel, ItemLimitSettingViewModel>(ServiceLifetime.Transient, provider => new ItemLimitSettingView(), provider => new ItemLimitSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<DropShelfSettings>>().Current, provider.GetRequiredService<IWritableOptions<DropShelfSettings>>()));
    }
}
