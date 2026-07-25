namespace Glance.Infinity;

public sealed record InfinityPageNavigationState(int PageIndex, int PageNumber, string PageTitle);

public sealed record InfinityPageNavigationVisibility(bool IsVisible);

public sealed record InfinityPageTitleUpdate(int PageIndex, string PageTitle);
