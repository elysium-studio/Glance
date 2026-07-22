using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Power.WinUI;

public sealed class PowerModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddModuleOptions<PowerSettings>("Power", "power.settings.dat", PowerJsonContext.Default);
        services.AddSingleton<ModuleResourceTextLocalizer<PowerModule>>();
        services.AddSingleton(provider => new PowerViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<PowerModule>>()));
        services.AddSingleton<IGlanceComponent, PowerComponent>();
        services
            .AddViewFor<LowBatteryThresholdSettingView, IGlanceModuleSettingViewModel, LowBatteryThresholdSettingViewModel>(ServiceLifetime.Transient, provider => new LowBatteryThresholdSettingView(), provider => new LowBatteryThresholdSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<PowerSettings>>().Current, provider.GetRequiredService<IWritableOptions<PowerSettings>>()))
            .AddViewFor<CriticalBatteryThresholdSettingView, IGlanceModuleSettingViewModel, CriticalBatteryThresholdSettingViewModel>(ServiceLifetime.Transient, provider => new CriticalBatteryThresholdSettingView(), provider => new CriticalBatteryThresholdSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<PowerSettings>>().Current, provider.GetRequiredService<IWritableOptions<PowerSettings>>()));
    }
}
