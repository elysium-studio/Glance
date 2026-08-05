using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Power.WinUI;

public sealed class PowerModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddModuleOptions<PowerSettings>("Power", "power.settings.dat", PowerJsonContext.Default);
        _ = services.AddSingleton<ModuleResourceTextLocalizer<PowerModule>>();
        _ = services.AddSingleton(provider => new PowerViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<PowerModule>>()));
        _ = services.AddSingleton<IGlanceComponent, PowerComponent>();
        _ = services
            .AddViewFor<LowBatteryThresholdSettingView, IGlanceModuleSettingViewModel, LowBatteryThresholdSettingViewModel>(ServiceLifetime.Transient, provider => new LowBatteryThresholdSettingView(), provider => new LowBatteryThresholdSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<PowerSettings>>().Current, provider.GetRequiredService<IWritableOptions<PowerSettings>>()))
            .AddViewFor<CriticalBatteryThresholdSettingView, IGlanceModuleSettingViewModel, CriticalBatteryThresholdSettingViewModel>(ServiceLifetime.Transient, provider => new CriticalBatteryThresholdSettingView(), provider => new CriticalBatteryThresholdSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<PowerSettings>>().Current, provider.GetRequiredService<IWritableOptions<PowerSettings>>()));
    }
}
