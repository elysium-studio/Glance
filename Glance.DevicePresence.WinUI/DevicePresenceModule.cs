using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.DevicePresence.WinUI;

public sealed class DevicePresenceModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddModuleOptions<DevicePresenceSettings>("DevicePresence", "device-presence.settings.dat", DevicePresenceJsonContext.Default);
        _ = services.AddSingleton<ModuleResourceTextLocalizer<DevicePresenceModule>>();
        _ = services.AddSingleton<IDevicePresenceService, WindowsDevicePresenceService>();
        _ = services.AddSingleton(provider => new DevicePresenceViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<DevicePresenceModule>>()));
        _ = services.AddSingleton<IGlanceComponent, DevicePresenceComponent>();
        _ = services.AddViewFor<DevicePresenceLowBatteryThresholdSettingView, IGlanceModuleSettingViewModel, DevicePresenceLowBatteryThresholdSettingViewModel>(ServiceLifetime.Transient, provider => new DevicePresenceLowBatteryThresholdSettingView(), provider => new DevicePresenceLowBatteryThresholdSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<DevicePresenceSettings>>().Current, provider.GetRequiredService<IWritableOptions<DevicePresenceSettings>>()));
    }
}
