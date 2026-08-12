using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;

namespace Glance.Network;

public sealed partial class NetworkViewModel(ITextLocalizer localizer) :
    ObservableObject
{
    private readonly ITextLocalizer localizer = localizer;

    [ObservableProperty]
    private string networkName = localizer.GetText("Disconnected");

    [ObservableProperty]
    private string connectionType = localizer.GetText("OtherConnection");

    [ObservableProperty]
    private string ipAddress = localizer.GetText("NoIpAddress");

    [ObservableProperty]
    private string downloadText = "0 B/s";

    [ObservableProperty]
    private string uploadText = "0 B/s";

    [ObservableProperty]
    private double downloadBytesPerSecond;

    [ObservableProperty]
    private double uploadBytesPerSecond;

    [ObservableProperty]
    private bool isConnected;

    public event EventHandler? MetricsUpdated;

    public void Update(NetworkSnapshot snapshot)
    {
        IsConnected = snapshot.IsConnected;
        NetworkName = snapshot.IsConnected
            ? string.IsNullOrWhiteSpace(snapshot.Name) ? localizer.GetText("UnknownNetwork") : snapshot.Name
            : localizer.GetText("Disconnected");
        ConnectionType = localizer.GetText(snapshot.Kind switch
        {
            NetworkConnectionKind.WiFi => "WiFiConnection",
            NetworkConnectionKind.Ethernet => "EthernetConnection",
            NetworkConnectionKind.Cellular => "CellularConnection",
            NetworkConnectionKind.Vpn => "VpnConnection",
            _ => "OtherConnection"
        });
        IpAddress = string.IsNullOrWhiteSpace(snapshot.IpAddress)
            ? localizer.GetText("NoIpAddress")
            : snapshot.IpAddress;
        DownloadBytesPerSecond = Math.Max(0, snapshot.DownloadBytesPerSecond);
        UploadBytesPerSecond = Math.Max(0, snapshot.UploadBytesPerSecond);
        DownloadText = FormatRate(snapshot.DownloadBytesPerSecond);
        UploadText = FormatRate(snapshot.UploadBytesPerSecond);
        MetricsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private static string FormatRate(double bytesPerSecond) => Math.Max(0, bytesPerSecond) switch
    {
        >= 1024d * 1024d * 1024d => $"{bytesPerSecond / (1024d * 1024d * 1024d):0.0} GB/s",
        >= 1024d * 1024d => $"{bytesPerSecond / (1024d * 1024d):0.0} MB/s",
        >= 1024d => $"{bytesPerSecond / 1024d:0} KB/s",
        _ => $"{bytesPerSecond:0} B/s"
    };
}

public readonly record struct NetworkSnapshot(bool IsConnected,
    string? Name,
    NetworkConnectionKind Kind,
    string? IpAddress,
    double DownloadBytesPerSecond,
    double UploadBytesPerSecond);
