using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.Infinity;

public sealed partial class InfinityViewModel :
    ObservableObject
{
    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    private int pageNumber;

    [ObservableProperty]
    private string pageTitle = string.Empty;

    public void Update(InfinityPageNavigationState state)
    {
        PageTitle = state.PageTitle;
        PageNumber = state.PageNumber;
        IsActive = state.IsActive;
        IsConnected = true;
    }

    public void Disconnect()
    {
        IsActive = false;
        IsConnected = false;
    }
}
