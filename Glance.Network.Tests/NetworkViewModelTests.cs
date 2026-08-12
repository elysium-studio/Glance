using Glance.Application.Abstractions;

namespace Glance.Network.Tests;

public sealed class NetworkViewModelTests
{
    [Fact]
    public void Update_ShowsConnectionDetails()
    {
        NetworkViewModel viewModel = new(new TestTextLocalizer());

        viewModel.Update(new NetworkSnapshot(true,
            "Studio Wi-Fi",
            NetworkConnectionKind.WiFi,
            "192.168.1.42",
            2.5 * 1024 * 1024,
            10 * 1024));

        Assert.True(viewModel.IsConnected);
        Assert.Equal("Studio Wi-Fi", viewModel.NetworkName);
        Assert.Equal("WiFiConnection", viewModel.ConnectionType);
        Assert.Equal("192.168.1.42", viewModel.IpAddress);
        Assert.Equal(2.5 * 1024 * 1024, viewModel.DownloadBytesPerSecond);
        Assert.Equal(10 * 1024, viewModel.UploadBytesPerSecond);
        Assert.Equal("2.5 MB/s", viewModel.DownloadText);
        Assert.Equal("10 KB/s", viewModel.UploadText);
    }

    [Fact]
    public void Update_ShowsDisconnectedFallbacks()
    {
        NetworkViewModel viewModel = new(new TestTextLocalizer());

        viewModel.Update(new NetworkSnapshot(false, null, NetworkConnectionKind.Other, null, 0, 0));

        Assert.False(viewModel.IsConnected);
        Assert.Equal("Disconnected", viewModel.NetworkName);
        Assert.Equal("NoIpAddress", viewModel.IpAddress);
        Assert.Equal("0 B/s", viewModel.DownloadText);
        Assert.Equal("0 B/s", viewModel.UploadText);
    }

    [Fact]
    public void AdapterViewModel_ShowsOnlyCurrentAdapter()
    {
        NetworkAdapterInfo ethernet = new("ethernet", "Ethernet", "Office LAN", "192.168.1.42", "1 Gbps");
        NetworkAdapterViewModel viewModel = new(new TestNetworkAdapterService(ethernet));

        viewModel.Refresh();

        Assert.Same(ethernet, viewModel.CurrentAdapter);
        Assert.Equal("Ethernet", viewModel.CurrentAdapterName);
        Assert.Equal("Office LAN · 192.168.1.42 · 1 Gbps", viewModel.CurrentAdapterDetail);
    }

    private sealed class TestTextLocalizer :
        ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) => key;
    }

    private sealed class TestNetworkAdapterService(NetworkAdapterInfo? adapter) :
        INetworkAdapterService
    {
        public NetworkAdapterInfo? GetCurrentAdapter() => adapter;
    }
}
