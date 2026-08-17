namespace Glance.QuickConvert.OnlineMedia;

using Glance.Application.Abstractions;

public static class YtDlpUrlMatcher
{
    private static readonly HashSet<string> supportedHosts =
    [
        with(StringComparer.OrdinalIgnoreCase),
        "bilibili.com",
        "dailymotion.com",
        "facebook.com",
        "instagram.com",
        "reddit.com",
        "soundcloud.com",
        "tiktok.com",
        "twitch.tv",
        "twitter.com",
        "vimeo.com",
        "x.com",
        "youtu.be",
        "youtube.com"
    ];
    private static readonly HashSet<string> mediaExtensions =
    [
        with(StringComparer.OrdinalIgnoreCase),
        ".aac", ".avi", ".flac", ".m3u8", ".m4a", ".m4v", ".mkv", ".mov", ".mp3", ".mp4", ".mpeg", ".mpg", ".ogg", ".opus", ".wav", ".webm"
    ];
    public static bool TryGetUri(string? content,
        out Uri uri)
    {
        if (Uri.TryCreate(content?.Trim(), UriKind.Absolute, out Uri? candidate) &&
            candidate.Scheme is "http" or "https")
        {
            uri = candidate;
            return true;
        }

        uri = null!;
        return false;
    }

    public static GlanceQuickConverterMatch Match(string? content)
    {
        if (!TryGetUri(content, out Uri uri))
        {
            return GlanceQuickConverterMatch.None;
        }

        string host = uri.IdnHost;

        if (host.Equals("open.spotify.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".spotify.com", StringComparison.OrdinalIgnoreCase))
        {
            return GlanceQuickConverterMatch.None;
        }

        if (supportedHosts.Any(candidate => host.Equals(candidate, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith('.' + candidate, StringComparison.OrdinalIgnoreCase)))
        {
            return GlanceQuickConverterMatch.Exact;
        }

        string extension = Path.GetExtension(uri.AbsolutePath);

        if (mediaExtensions.Contains(extension))
        {
            return GlanceQuickConverterMatch.Supported;
        }

        return GlanceQuickConverterMatch.None;
    }

    public static bool IsSupported(string? content) => Match(content) != GlanceQuickConverterMatch.None;
}
