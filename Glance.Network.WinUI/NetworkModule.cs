using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Network.WinUI;

public sealed class NetworkModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<NetworkModule>>();
        _ = services.AddSingleton<NetworkSnapshotReader>();
        _ = services.AddSingleton<INetworkAdapterService, WindowsNetworkAdapterService>();
        _ = services.AddSingleton(provider => new NetworkViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<NetworkModule>>()));
        _ = services.AddSingleton<NetworkAdapterViewModel>();
        _ = services.AddSingleton<IGlanceComponent, NetworkComponent>();
        _ = services.AddSingleton<IGlanceComponent, NetworkAdapterComponent>();
    }
}
