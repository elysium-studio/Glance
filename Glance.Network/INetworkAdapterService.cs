namespace Glance.Network;

public interface INetworkAdapterService
{
    NetworkAdapterInfo? GetCurrentAdapter();
}
