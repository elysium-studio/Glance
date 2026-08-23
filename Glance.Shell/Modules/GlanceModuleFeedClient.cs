using System.Text.Json;

namespace Glance.Shell;

public sealed class GlanceModuleFeedClient :
    IGlanceModuleFeedClient
{
    private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient httpClient;
    private readonly IGlanceModuleFeedValidator validator;

    public GlanceModuleFeedClient(HttpClient httpClient, IGlanceModuleFeedValidator validator)
    {
        this.httpClient = httpClient;
        this.validator = validator;
    }

    public async Task<GlanceModuleFeed?> GetAsync(GlanceModuleFeedSource source, CancellationToken cancellationToken = default)
    {
        await using Stream stream = source.Uri.IsFile ? File.OpenRead(source.Uri.LocalPath) : await httpClient.GetStreamAsync(source.Uri, cancellationToken);
        GlanceModuleFeed? feed = await JsonSerializer.DeserializeAsync<GlanceModuleFeed>(stream, serializerOptions, cancellationToken);

        if (feed is not null)
        {
            validator.Validate(feed, source);
        }

        return feed;
    }
}
