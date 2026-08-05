using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.SystemMonitor.WinUI;

public sealed class SystemMonitorModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddModuleOptions<SystemMonitorSettings>("SystemMonitor", "system-monitor.settings.dat", SystemMonitorJsonContext.Default);
        _ = services.AddSingleton<ModuleResourceTextLocalizer<SystemMonitorModule>>();
        _ = services.AddSingleton(provider => new SystemMonitorViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<SystemMonitorModule>>()));
        _ = services.AddSingleton<IGlanceComponent, SystemMonitorComponent>();
        _ = services.AddViewFor<PerformanceChartsSettingView, IGlanceModuleSettingViewModel, PerformanceChartsSettingViewModel>(ServiceLifetime.Transient, provider => new PerformanceChartsSettingView(), provider => new PerformanceChartsSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<SystemMonitorSettings>>().Current, provider.GetRequiredService<IWritableOptions<SystemMonitorSettings>>()));
        _ = services.AddViewFor<RefreshIntervalSettingView, IGlanceModuleSettingViewModel, RefreshIntervalSettingViewModel>(ServiceLifetime.Transient, provider => new RefreshIntervalSettingView(), provider => new RefreshIntervalSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<SystemMonitorSettings>>().Current, provider.GetRequiredService<IWritableOptions<SystemMonitorSettings>>()));
    }
}
