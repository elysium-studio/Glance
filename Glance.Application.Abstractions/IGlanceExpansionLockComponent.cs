namespace Glance.Application.Abstractions;

public interface IGlanceExpansionLockComponent
{
    bool IsExpansionLocked { get; }

    event EventHandler? ExpansionLockChanged;

    void DismissExpansionLock();
}
