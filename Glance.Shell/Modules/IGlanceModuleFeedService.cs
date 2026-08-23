namespace Glance.Shell;

public interface IGlanceModuleFeedService
{
    event EventHandler? FeedChanged;

    IReadOnlyList<GlanceModuleFeedItem> Modules { get; }

    IReadOnlyList<GlanceModuleFeedStatus> Sources { get; }

    bool IsAvailable { get; }

    bool IsUsingCache { get; }

    DateTimeOffset? LastUpdated { get; }

    string? ErrorMessage { get; }

    bool IsSourceAvailable(string feedId);

    Task RefreshAsync(CancellationToken cancellationToken = default);

}
