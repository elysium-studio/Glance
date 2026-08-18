using Elysium.Application.Abstractions;

namespace Glance.Shell;

public sealed class GlanceQuickConverterPreferences :
    IGlanceQuickConverterPreferences
{
    private readonly Dictionary<string, bool> converters;
    private readonly GlanceSettings settings;
    private readonly IWritableOptions<GlanceSettings> writer;
    private readonly SemaphoreSlim synchronization = new(1, 1);

    public GlanceQuickConverterPreferences(GlanceSettings settings, IWritableOptions<GlanceSettings> writer)
    {
        this.settings = settings;
        this.writer = writer;
        converters = settings.Converters
            .Where(converter => !string.IsNullOrWhiteSpace(converter.Id))
            .GroupBy(converter => converter.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().IsEnabled, StringComparer.OrdinalIgnoreCase);
        settings.Converters = CreateSnapshot();
    }

    public bool IsEnabled(string converterId)
    {
        lock (converters)
        {
            return !converters.TryGetValue(converterId, out bool enabled) || enabled;
        }
    }

    public async Task RegisterAsync(IEnumerable<string> converterIds, CancellationToken cancellationToken = default)
    {
        string[] ids = [.. converterIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase)];
        await synchronization.WaitAsync(cancellationToken);

        try
        {
            Dictionary<string, bool> previous;

            lock (converters)
            {
                previous = new Dictionary<string, bool>(converters, StringComparer.OrdinalIgnoreCase);

                foreach (string id in ids)
                {
                    converters.TryAdd(id, true);
                }

                if (previous.Count == converters.Count)
                {
                    return;
                }
            }

            try
            {
                await SaveAsync(cancellationToken);
            }
            catch
            {
                Restore(previous);
                throw;
            }
        }
        finally
        {
            _ = synchronization.Release();
        }
    }

    public async Task RemoveAsync(IEnumerable<string> converterIds, CancellationToken cancellationToken = default)
    {
        HashSet<string> ids = [with(StringComparer.OrdinalIgnoreCase), .. converterIds];
        await synchronization.WaitAsync(cancellationToken);

        try
        {
            Dictionary<string, bool> previous;

            lock (converters)
            {
                previous = new Dictionary<string, bool>(converters, StringComparer.OrdinalIgnoreCase);

                foreach (string id in ids)
                {
                    _ = converters.Remove(id);
                }

                if (previous.Count == converters.Count)
                {
                    return;
                }
            }

            try
            {
                await SaveAsync(cancellationToken);
            }
            catch
            {
                Restore(previous);
                throw;
            }
        }
        finally
        {
            _ = synchronization.Release();
        }
    }

    public async Task SetEnabledAsync(string converterId, bool enabled, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(converterId);
        await synchronization.WaitAsync(cancellationToken);

        try
        {
            bool existed;
            bool previous;

            lock (converters)
            {
                existed = converters.TryGetValue(converterId, out previous);

                if (existed && previous == enabled)
                {
                    return;
                }

                converters[converterId] = enabled;
                settings.Converters = CreateSnapshot();
            }

            try
            {
                await SaveAsync(cancellationToken);
            }
            catch
            {
                lock (converters)
                {
                    if (existed)
                    {
                        converters[converterId] = previous;
                    }
                    else
                    {
                        _ = converters.Remove(converterId);
                    }

                    settings.Converters = CreateSnapshot();
                }

                throw;
            }
        }
        finally
        {
            _ = synchronization.Release();
        }
    }

    private void Restore(Dictionary<string, bool> previous)
    {
        lock (converters)
        {
            converters.Clear();

            foreach ((string id, bool enabled) in previous)
            {
                converters.Add(id, enabled);
            }

            settings.Converters = CreateSnapshot();
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        List<GlanceQuickConverterPreference> snapshot;

        lock (converters)
        {
            snapshot = CreateSnapshot();
            settings.Converters = snapshot;
        }

        await writer.WriteAsync(value => value.Converters = [.. snapshot.Select(Clone)], cancellationToken);
    }

    private List<GlanceQuickConverterPreference> CreateSnapshot() => [.. converters.OrderBy(converter => converter.Key, StringComparer.OrdinalIgnoreCase).Select(converter => new GlanceQuickConverterPreference { Id = converter.Key, IsEnabled = converter.Value })];

    private static GlanceQuickConverterPreference Clone(GlanceQuickConverterPreference preference) => new() { Id = preference.Id, IsEnabled = preference.IsEnabled };
}
