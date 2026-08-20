namespace Glance.Application.Abstractions;

public interface IGlanceInspectionAction
{
    string Id { get; }

    string DisplayName { get; }

    string Glyph { get; }

    bool IsDestructive { get; }

    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
