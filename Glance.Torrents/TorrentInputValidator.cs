namespace Glance.Torrents;

public static class TorrentInputValidator
{
    public static bool IsValidTorrentPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        string.Equals(Path.GetExtension(path.Trim()), ".torrent", StringComparison.OrdinalIgnoreCase);

    public static bool IsValidMagnet(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri) ||
            !string.Equals(uri.Scheme, "magnet", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string query = uri.Query.TrimStart('?');

        foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = pair.Split('=', 2);

            if (parts.Length != 2 || !string.Equals(Uri.UnescapeDataString(parts[0]), "xt", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string exactTopic = Uri.UnescapeDataString(parts[1]);

            if (exactTopic.StartsWith("urn:btih:", StringComparison.OrdinalIgnoreCase))
            {
                string hash = exactTopic[9..];
                return hash.Length == 40 && hash.All(Uri.IsHexDigit) ||
                    hash.Length == 32 && hash.All(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '2' and <= '7');
            }

            if (exactTopic.StartsWith("urn:btmh:", StringComparison.OrdinalIgnoreCase) && exactTopic.Length > 9)
            {
                return true;
            }
        }

        return false;
    }
}
