namespace Glance.Torrents;

public sealed class TorrentCompletionTracker(IEnumerable<string>? alreadyNotified = null)
{
    private readonly HashSet<string> notified = new(alreadyNotified ?? [], StringComparer.OrdinalIgnoreCase);

    public bool TryMarkCompleted(string torrentId) => notified.Add(torrentId);

    public bool IsCompleted(string torrentId) => notified.Contains(torrentId);
}
