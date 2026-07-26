namespace Glance.KeepAwake;

public interface IKeepAwakeService
{
    bool IsActive { get; }

    Task<bool> SetActiveAsync(bool isActive,
        CancellationToken cancellationToken = default);
}
