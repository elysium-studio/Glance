namespace Glance.Application.Abstractions;

public interface IGlanceComponent
{
    string Id { get; }

    int Order { get; }

    object CompactContent { get; }

    object ExpandedContent { get; }
}
