using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.DevicePresence.WinUI;

public sealed class DevicePresenceModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddModuleOptions<DevicePresenceSettings>("DevicePresence", "device-presence.settings.dat", DevicePresenceJsonContext.Default);
        services.AddSingleton<ModuleResourceTextLocalizer<DevicePresenceModule>>();
        services.AddSingleton<IDevicePresenceService, WindowsDevicePresenceService>();
        services.AddSingleton(provider => new DevicePresenceViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<DevicePresenceModule>>()));
        services.AddSingleton<IGlanceComponent, DevicePresenceComponent>();
        services.AddViewFor<DevicePresenceLowBatteryThresholdSettingView, IGlanceModuleSettingViewModel, DevicePresenceLowBatteryThresholdSettingViewModel>(ServiceLifetime.Transient, provider => new DevicePresenceLowBatteryThresholdSettingView(), provider => new DevicePresenceLowBatteryThresholdSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<DevicePresenceSettings>>().Current, provider.GetRequiredService<IWritableOptions<DevicePresenceSettings>>()));
    }
}
