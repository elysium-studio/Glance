using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.SystemMonitor.WinUI;

public sealed class SystemMonitorModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddModuleOptions<SystemMonitorSettings>("SystemMonitor", "system-monitor.settings.dat", SystemMonitorJsonContext.Default);
        services.AddSingleton<ModuleResourceTextLocalizer<SystemMonitorModule>>();
        services.AddSingleton(provider => new SystemMonitorViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<SystemMonitorModule>>()));
        services.AddSingleton<IGlanceComponent, SystemMonitorComponent>();
        services.AddViewFor<RefreshIntervalSettingView, IGlanceModuleSettingViewModel, RefreshIntervalSettingViewModel>(ServiceLifetime.Transient, provider => new RefreshIntervalSettingView(), provider => new RefreshIntervalSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<SystemMonitorSettings>>().Current, provider.GetRequiredService<IWritableOptions<SystemMonitorSettings>>()));
    }
}
