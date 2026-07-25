using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.Infinity;

public sealed partial class InfinityViewModel :
    ObservableObject
{
    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private bool isAvailable;

    [ObservableProperty]
    private int pageNumber;

    [ObservableProperty]
    private string pageTitle = string.Empty;

    public void Update(InfinityPageNavigationState state)
    {
        PageTitle = state.PageTitle;
        PageNumber = state.PageNumber;
        IsConnected = true;
    }

    public void Disconnect()
    {
        IsAvailable = false;
        IsConnected = false;
    }
}
