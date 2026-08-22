using System.Collections.Concurrent;

namespace Glance.Settings;

internal static class GlanceSettingsFileLock
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);

    public static SemaphoreSlim Get(string path) => Locks.GetOrAdd(Path.GetFullPath(path), _ => new SemaphoreSlim(1, 1));
}
