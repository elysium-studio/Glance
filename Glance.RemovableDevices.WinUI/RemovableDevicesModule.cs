using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.RemovableDevices.WinUI;

public sealed class RemovableDevicesModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ModuleResourceTextLocalizer<RemovableDevicesModule>>();
        services.AddSingleton<IRemovableDeviceService, WindowsRemovableDeviceService>();
        services.AddSingleton(provider => new RemovableDevicesViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<RemovableDevicesModule>>()));
        services.AddSingleton<IGlanceComponent, RemovableDevicesComponent>();
    }
}
