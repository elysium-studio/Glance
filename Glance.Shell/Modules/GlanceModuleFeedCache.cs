using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace Glance.Shell;

public sealed class GlanceModuleFeedCache :
    IGlanceModuleFeedCache
{
    private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IGlanceModuleFeedValidator validator;
    private readonly string cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance", "Modules", "Feeds");

    public GlanceModuleFeedCache(IGlanceModuleFeedValidator validator) => this.validator = validator;

    public async Task<GlanceModuleFeed?> ReadAsync(GlanceModuleFeedSource source, CancellationToken cancellationToken = default)
    {
        string cachePath = GetCachePath(source);

        if (!File.Exists(cachePath))
        {
            return null;
        }

        try
        {
            await using FileStream stream = File.OpenRead(cachePath);
            GlanceModuleFeed? feed = await JsonSerializer.DeserializeAsync<GlanceModuleFeed>(stream, serializerOptions, cancellationToken);

            if (feed is not null)
            {
                validator.Validate(feed, source);
            }

            return feed;
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
        {
            return null;
        }
    }

    public async Task WriteAsync(GlanceModuleFeedSource source, GlanceModuleFeed feed, CancellationToken cancellationToken = default)
    {
        string cachePath = GetCachePath(source);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        string temporaryPath = $"{cachePath}.{Guid.NewGuid():N}.writing";
        await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(feed, serializerOptions), cancellationToken);
        File.Move(temporaryPath, cachePath, true);
    }

    private string GetCachePath(GlanceModuleFeedSource source) => Path.Combine(cacheDirectory, $"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.Id)))}.json");
}
