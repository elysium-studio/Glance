using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.Network;

public sealed partial class NetworkAdapterViewModel(INetworkAdapterService adapterService) :
    ObservableObject
{
    private readonly INetworkAdapterService adapterService = adapterService;
    [ObservableProperty]
    private bool hasAdapter;

    [ObservableProperty]
    private NetworkAdapterInfo? currentAdapter;

    [ObservableProperty]
    private string currentAdapterName = "No network adapter";

    [ObservableProperty]
    private string currentAdapterDetail = string.Empty;

    public void Refresh()
    {
        CurrentAdapter = adapterService.GetCurrentAdapter();
        HasAdapter = CurrentAdapter is not null;
        CurrentAdapterName = CurrentAdapter?.Name ?? "No network adapter";
        CurrentAdapterDetail = CurrentAdapter?.DetailText ?? string.Empty;
    }
}
