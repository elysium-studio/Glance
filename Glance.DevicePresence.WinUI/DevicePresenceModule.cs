using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.DevicePresence.WinUI;

public sealed class DevicePresenceModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ModuleResourceTextLocalizer<DevicePresenceModule>>();
        services.AddSingleton<IDevicePresenceService, WindowsDevicePresenceService>();
        services.AddSingleton(provider => new DevicePresenceViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<DevicePresenceModule>>()));
        services.AddSingleton<IGlanceComponent, DevicePresenceComponent>();
    }
}
