using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.Infinity;

public sealed partial class InfinityViewModel :
    ObservableObject
{
    private readonly IInfinityPageTitleUpdater pageTitleUpdater;

    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private bool isAvailable;

    [ObservableProperty]
    private int pageIndex;

    [ObservableProperty]
    private int pageNumber;

    [ObservableProperty]
    private string pageTitle = string.Empty;

    [ObservableProperty]
    private string editingTitle = string.Empty;

    [ObservableProperty]
    private bool isEditing;

    [ObservableProperty]
    private bool isSavingTitle;

    public InfinityViewModel(IInfinityPageTitleUpdater pageTitleUpdater)
    {
        this.pageTitleUpdater = pageTitleUpdater;
    }

    public void Update(InfinityPageNavigationState state)
    {
        if (IsEditing && PageIndex != state.PageIndex)
        {
            IsEditing = false;
        }

        PageIndex = state.PageIndex;
        PageTitle = state.PageTitle;
        PageNumber = state.PageNumber;
        IsConnected = true;
    }

    public void BeginEditing()
    {
        EditingTitle = PageTitle;
        IsEditing = true;
    }

    public async Task CommitEditAsync()
    {
        if (IsSavingTitle)
        {
            return;
        }

        IsSavingTitle = true;

        try
        {
            if (await pageTitleUpdater.UpdatePageTitleAsync(PageIndex, EditingTitle.Trim()))
            {
                IsEditing = false;
            }
        }
        finally
        {
            IsSavingTitle = false;
        }
    }

    public void CancelEditing() => IsEditing = false;

    public void Disconnect()
    {
        IsAvailable = false;
        IsConnected = false;
        IsEditing = false;
    }
}
