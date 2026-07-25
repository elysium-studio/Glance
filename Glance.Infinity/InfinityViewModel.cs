using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.Infinity;

public sealed partial class InfinityViewModel :
    ObservableObject
{
    private readonly IInfinityPageTitleUpdater pageTitleUpdater;
    private bool isInteracting;
    private bool isSurfaceVisible;

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

    public void BeginInteraction()
    {
        isInteracting = true;
        IsAvailable = true;
    }

    public void EndInteraction()
    {
        isInteracting = false;
        DismissIfIdle();
    }

    public void SetSurfaceVisibility(bool isVisible)
    {
        isSurfaceVisible = isVisible;

        if (isVisible)
        {
            IsAvailable = true;
        }
    }

    public void DismissIfIdle()
    {
        if (!isSurfaceVisible && !isInteracting && !IsEditing)
        {
            IsAvailable = false;
        }
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
        isInteracting = false;
        isSurfaceVisible = false;
        IsAvailable = false;
        IsConnected = false;
        IsEditing = false;
    }

    partial void OnIsEditingChanged(bool value)
    {
        if (!value)
        {
            DismissIfIdle();
        }
    }
}
