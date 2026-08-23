using Microsoft.Extensions.Logging;

namespace Glance.Shell;

public sealed class GlanceModuleFeedService :
    IGlanceModuleFeedService
{
    private readonly IGlanceModuleFeedCache cache;
    private readonly IGlanceModuleFeedClient client;
    private readonly ILogger<GlanceModuleFeedService> logger;
    private readonly IGlanceModuleFeedSourceProvider sourceProvider;
    private readonly SemaphoreSlim synchronization = new(1, 1);
    private IReadOnlyList<GlanceModuleFeedItem> modules = [];
    private IReadOnlyList<GlanceModuleFeedStatus> sources = [];

    public GlanceModuleFeedService(IGlanceModuleFeedClient client, IGlanceModuleFeedCache cache, IGlanceModuleFeedSourceProvider sourceProvider, ILogger<GlanceModuleFeedService> logger)
    {
        this.client = client;
        this.cache = cache;
        this.sourceProvider = sourceProvider;
        this.logger = logger;
    }

    public event EventHandler? FeedChanged;

    public IReadOnlyList<GlanceModuleFeedItem> Modules => modules;

    public IReadOnlyList<GlanceModuleFeedStatus> Sources => sources;

    public bool IsAvailable { get; private set; }

    public bool IsUsingCache { get; private set; }

    public DateTimeOffset? LastUpdated { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool IsSourceAvailable(string feedId) => sources.FirstOrDefault(status => string.Equals(status.Source.Id, feedId, StringComparison.OrdinalIgnoreCase))?.IsAvailable == true;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await synchronization.WaitAsync(cancellationToken);

        try
        {
            List<GlanceModuleFeedStatus> currentSources = [];
            List<GlanceModuleFeedItem> currentModules = [];
            List<DateTimeOffset> updateTimes = [];

            foreach (GlanceModuleFeedSource source in sourceProvider.GetSources().Where(source => source.IsEnabled))
            {
                GlanceModuleFeed? sourceFeed = null;
                bool isAvailable = false;
                bool isUsingCache = false;
                string? errorMessage = null;

                try
                {
                    sourceFeed = await client.GetAsync(source, cancellationToken);

                    if (sourceFeed is not null)
                    {
                        isAvailable = true;

                        try
                        {
                            await cache.WriteAsync(source, sourceFeed, cancellationToken);
                        }
                        catch
                        {
                            logger.LogWarning("The module feed cache could not be updated for {FeedId}", source.Id);
                        }
                    }
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "The module feed could not be refreshed for {FeedId}", source.Id);
                    errorMessage = exception.Message;
                    sourceFeed = await cache.ReadAsync(source, cancellationToken);
                    isUsingCache = sourceFeed is not null;
                }

                if (sourceFeed is not null)
                {
                    foreach (GlanceModuleFeedItem module in sourceFeed.Modules.Where(module => !module.IsRevoked))
                    {
                        module.FeedId = source.Id;
                        currentModules.Add(module);
                    }

                    updateTimes.Add(sourceFeed.GeneratedAt);
                }

                currentSources.Add(new GlanceModuleFeedStatus(source, isAvailable, isUsingCache, errorMessage));
            }

            sources = currentSources;
            modules = [.. currentModules.GroupBy(module => module.Id, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).OrderBy(module => module.CategoryOrder).ThenBy(module => module.Order)];
            IsAvailable = sources.Any(source => source.IsAvailable);
            IsUsingCache = sources.Any(source => source.IsUsingCache);
            ErrorMessage = string.Join(Environment.NewLine, sources.Where(source => !string.IsNullOrWhiteSpace(source.ErrorMessage)).Select(source => $"{source.Source.DisplayName}: {source.ErrorMessage}"));
            LastUpdated = updateTimes.Count > 0 ? updateTimes.Max() : null;
        }
        finally
        {
            _ = synchronization.Release();
        }

        FeedChanged?.Invoke(this, EventArgs.Empty);
    }

}
