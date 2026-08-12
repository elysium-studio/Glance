namespace Glance.Torrents;

public static class TorrentDuplicateNaming
{
    public static string GetAvailableName(string torrentName,
        Func<string, bool> isUnavailable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(torrentName);
        ArgumentNullException.ThrowIfNull(isUnavailable);

        for (int copyNumber = 0; ; copyNumber++)
        {
            string candidate = copyNumber == 0
                ? torrentName
                : $"{torrentName} ({copyNumber})";

            if (!isUnavailable(candidate))
            {
                return candidate;
            }
        }
    }
}
