namespace Glance.Shell;

public interface IReorderableSettingViewModel :
    ISettingViewModel
{
    bool CanReorder { get; }

    void BeginReordering();

    Task CompleteReorderingAsync();

    void CancelReordering();
}
