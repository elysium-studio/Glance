using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.RemovableDevices.WinUI;

public sealed class RemovableDevicesModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<RemovableDevicesModule>>();
        _ = services.AddSingleton<IRemovableDeviceService, WindowsRemovableDeviceService>();
        _ = services.AddSingleton(provider => new RemovableDevicesViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<RemovableDevicesModule>>()));
        _ = services.AddSingleton<IGlanceComponent, RemovableDevicesComponent>();
    }
}
