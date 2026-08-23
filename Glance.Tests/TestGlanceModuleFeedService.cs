using Glance.Shell;

namespace Glance.Tests;

internal sealed class TestGlanceModuleFeedService :
    IGlanceModuleFeedService
{
    public event EventHandler? FeedChanged;

    public IReadOnlyList<GlanceModuleFeedItem> Modules { get; set; } = [];

    public IReadOnlyList<GlanceModuleFeedStatus> Sources { get; set; } = [];

    public bool IsAvailable { get; set; }

    public bool IsUsingCache { get; set; }

    public DateTimeOffset? LastUpdated { get; set; }

    public string? ErrorMessage { get; set; }

    public bool IsSourceAvailable(string feedId) => Sources.FirstOrDefault(source => string.Equals(source.Source.Id, feedId, StringComparison.OrdinalIgnoreCase))?.IsAvailable == true;

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        FeedChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
}
