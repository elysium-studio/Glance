namespace Glance.Infinity;

public interface IInfinityPageTitleUpdater
{
    ValueTask<bool> UpdatePageTitleAsync(int pageIndex, string pageTitle, CancellationToken cancellationToken = default);
}
