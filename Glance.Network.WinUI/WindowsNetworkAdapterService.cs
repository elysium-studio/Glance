using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Windows.Networking.Connectivity;

namespace Glance.Network.WinUI;

public sealed class WindowsNetworkAdapterService :
    INetworkAdapterService
{
    public NetworkAdapterInfo? GetCurrentAdapter()
    {
        Guid? activeAdapterId = NetworkInformation.GetInternetConnectionProfile()?.NetworkAdapter?.NetworkAdapterId;
        if (activeAdapterId is null)
        {
            return null;
        }

        Dictionary<Guid, ConnectionProfile> profiles = NetworkInformation.GetConnectionProfiles()
            .Where(profile => profile.NetworkAdapter is not null)
            .GroupBy(profile => profile.NetworkAdapter.NetworkAdapterId)
            .ToDictionary(group => group.Key, group => group.First());

        NetworkInterface? adapter = NetworkInterface.GetAllNetworkInterfaces()
            .Where(IsUsefulAdapter)
            .FirstOrDefault(adapter => Guid.TryParse(adapter.Id, out Guid adapterId) && adapterId == activeAdapterId);

        return adapter is null ? null : CreateAdapter(adapter, profiles);
    }

    private static bool IsUsefulAdapter(NetworkInterface adapter) =>
        adapter.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel &&
        adapter.OperationalStatus is OperationalStatus.Up or OperationalStatus.Down;

    private static NetworkAdapterInfo CreateAdapter(NetworkInterface adapter,
        IReadOnlyDictionary<Guid, ConnectionProfile> profiles)
    {
        _ = Guid.TryParse(adapter.Id, out Guid adapterId);
        _ = profiles.TryGetValue(adapterId, out ConnectionProfile? profile);
        string address = adapter.GetIPProperties().UnicastAddresses
            .Where(item => item.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(item => item.Address.ToString())
            .FirstOrDefault() ?? string.Empty;
        return new NetworkAdapterInfo(adapter.Id,
            adapter.Name,
            profile?.ProfileName ?? GetConnectionType(adapter.NetworkInterfaceType),
            address,
            FormatLinkSpeed(adapter.Speed));
    }

    private static string GetConnectionType(NetworkInterfaceType type) => type switch
    {
        NetworkInterfaceType.Wireless80211 => "Wi-Fi",
        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.FastEthernetT => "Ethernet",
        NetworkInterfaceType.Ppp => "VPN",
        _ => "Network"
    };

    private static string FormatLinkSpeed(long bitsPerSecond) => Math.Max(0, bitsPerSecond) switch
    {
        >= 1_000_000_000 => $"{bitsPerSecond / 1_000_000_000d:0.#} Gbps",
        >= 1_000_000 => $"{bitsPerSecond / 1_000_000d:0.#} Mbps",
        >= 1_000 => $"{bitsPerSecond / 1_000d:0.#} Kbps",
        > 0 => $"{bitsPerSecond} bps",
        _ => string.Empty
    };
}
