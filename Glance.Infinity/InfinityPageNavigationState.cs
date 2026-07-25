namespace Glance.Infinity;

public sealed record InfinityPageNavigationState(int PageIndex, int PageNumber, string PageTitle);

public sealed record InfinityPageNavigationVisibility(bool IsVisible);
