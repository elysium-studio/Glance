using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Windows.Networking.Connectivity;

namespace Glance.Network.WinUI;

public sealed class NetworkSnapshotReader
{
    private string? previousInterfaceId;
    private long previousBytesReceived;
    private long previousBytesSent;
    private long previousTimestamp;

    public NetworkSnapshot Read()
    {
        ConnectionProfile? profile = GetInternetProfile();
        NetworkInterface? networkInterface = FindActiveInterface(profile);
        bool connected = IsConnected(profile, networkInterface);

        if (!connected || networkInterface is null)
        {
            ResetCounters();
            return new NetworkSnapshot(false, null, NetworkConnectionKind.Other, null, 0, 0);
        }

        (double download, double upload) = ReadRates(networkInterface);
        return new NetworkSnapshot(true,
            string.IsNullOrWhiteSpace(profile?.ProfileName) ? networkInterface.Name : profile.ProfileName,
            GetConnectionKind(profile, networkInterface),
            GetIpAddress(networkInterface),
            download,
            upload);
    }

    private static ConnectionProfile? GetInternetProfile()
    {
        try
        {
            return NetworkInformation.GetInternetConnectionProfile();
        }
        catch
        {
            return null;
        }
    }

    private static NetworkInterface? FindActiveInterface(ConnectionProfile? profile)
    {
        Guid? adapterId = profile?.NetworkAdapter?.NetworkAdapterId;
        NetworkInterface[] candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(item => item.OperationalStatus == OperationalStatus.Up &&
                           item.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .ToArray();

        if (adapterId is Guid id)
        {
            NetworkInterface? match = candidates.FirstOrDefault(item =>
                Guid.TryParse(item.Id.Trim('{', '}'), out Guid interfaceId) && interfaceId == id);

            if (match is not null)
            {
                return match;
            }
        }

        return candidates
            .Where(item => item.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .OrderByDescending(HasDefaultGateway)
            .ThenByDescending(item => item.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            .FirstOrDefault(item => GetIpAddress(item) is not null);
    }

    private static bool HasDefaultGateway(NetworkInterface networkInterface)
    {
        try
        {
            return networkInterface.GetIPProperties().GatewayAddresses.Any(gateway =>
                gateway.Address.AddressFamily == AddressFamily.InterNetwork &&
                !gateway.Address.Equals(IPAddress.Any));
        }
        catch (NetworkInformationException)
        {
            return false;
        }
    }

    private static bool IsConnected(ConnectionProfile? profile,
        NetworkInterface? networkInterface)
    {
        try
        {
            return profile?.GetNetworkConnectivityLevel() is NetworkConnectivityLevel.InternetAccess or
                NetworkConnectivityLevel.ConstrainedInternetAccess or
                NetworkConnectivityLevel.LocalAccess || networkInterface is not null;
        }
        catch
        {
            return networkInterface is not null;
        }
    }

    private static NetworkConnectionKind GetConnectionKind(ConnectionProfile? profile,
        NetworkInterface networkInterface)
    {
        if (profile?.IsWlanConnectionProfile == true || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
        {
            return NetworkConnectionKind.WiFi;
        }

        if (profile?.IsWwanConnectionProfile == true)
        {
            return NetworkConnectionKind.Cellular;
        }

        return networkInterface.NetworkInterfaceType switch
        {
            NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.FastEthernetT => NetworkConnectionKind.Ethernet,
            NetworkInterfaceType.Ppp => NetworkConnectionKind.Vpn,
            _ => NetworkConnectionKind.Other
        };
    }

    private static string? GetIpAddress(NetworkInterface networkInterface)
    {
        try
        {
            return networkInterface.GetIPProperties().UnicastAddresses
                .Select(address => address.Address)
                .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork &&
                                           !IPAddress.IsLoopback(address) &&
                                           !address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
                ?.ToString();
        }
        catch (NetworkInformationException)
        {
            return null;
        }
    }

    private (double Download, double Upload) ReadRates(NetworkInterface networkInterface)
    {
        try
        {
            IPv4InterfaceStatistics statistics = networkInterface.GetIPv4Statistics();
            long timestamp = Stopwatch.GetTimestamp();

            if (!string.Equals(previousInterfaceId, networkInterface.Id, StringComparison.OrdinalIgnoreCase) || previousTimestamp == 0)
            {
                previousInterfaceId = networkInterface.Id;
                previousBytesReceived = statistics.BytesReceived;
                previousBytesSent = statistics.BytesSent;
                previousTimestamp = timestamp;
                return (0, 0);
            }

            double elapsed = Stopwatch.GetElapsedTime(previousTimestamp, timestamp).TotalSeconds;
            double download = elapsed <= 0 || statistics.BytesReceived < previousBytesReceived
                ? 0
                : (statistics.BytesReceived - previousBytesReceived) / elapsed;
            double upload = elapsed <= 0 || statistics.BytesSent < previousBytesSent
                ? 0
                : (statistics.BytesSent - previousBytesSent) / elapsed;

            previousBytesReceived = statistics.BytesReceived;
            previousBytesSent = statistics.BytesSent;
            previousTimestamp = timestamp;
            return (download, upload);
        }
        catch (NetworkInformationException)
        {
            ResetCounters();
            return (0, 0);
        }
    }

    private void ResetCounters()
    {
        previousInterfaceId = null;
        previousBytesReceived = 0;
        previousBytesSent = 0;
        previousTimestamp = 0;
    }
}
