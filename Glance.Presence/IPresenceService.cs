namespace Glance.Presence;

public interface IPresenceService
{
    bool IsActive { get; }

    Task<bool> SetActiveAsync(bool isActive,
        CancellationToken cancellationToken = default);
}
