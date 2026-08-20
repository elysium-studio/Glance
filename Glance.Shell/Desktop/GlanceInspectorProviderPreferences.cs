using Elysium.Application.Abstractions;

namespace Glance.Shell;

public sealed class GlanceInspectorProviderPreferences :
    IGlanceInspectorProviderPreferences
{
    private readonly Dictionary<string, bool> providers;
    private readonly GlanceSettings settings;
    private readonly IWritableOptions<GlanceSettings> writer;
    private readonly SemaphoreSlim synchronization = new(1, 1);

    public GlanceInspectorProviderPreferences(GlanceSettings settings, IWritableOptions<GlanceSettings> writer)
    {
        this.settings = settings;
        this.writer = writer;
        providers = settings.InspectorProviders.Where(provider => !string.IsNullOrWhiteSpace(provider.Id)).GroupBy(provider => provider.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First().IsEnabled, StringComparer.OrdinalIgnoreCase);
        settings.InspectorProviders = CreateSnapshot();
    }

    public bool IsEnabled(string providerId)
    {
        lock (providers)
        {
            return !providers.TryGetValue(providerId, out bool enabled) || enabled;
        }
    }

    public Task RegisterAsync(IEnumerable<string> providerIds, CancellationToken cancellationToken = default) => UpdateAsync(providerIds, true, false, cancellationToken);

    public Task RemoveAsync(IEnumerable<string> providerIds, CancellationToken cancellationToken = default) => UpdateAsync(providerIds, false, true, cancellationToken);

    public async Task SetEnabledAsync(string providerId, bool enabled, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        await synchronization.WaitAsync(cancellationToken);

        try
        {
            Dictionary<string, bool> previous;

            lock (providers)
            {
                previous = new Dictionary<string, bool>(providers, StringComparer.OrdinalIgnoreCase);

                if (providers.TryGetValue(providerId, out bool current) && current == enabled)
                {
                    return;
                }

                providers[providerId] = enabled;
            }

            await SaveOrRestoreAsync(previous, cancellationToken);
        }
        finally
        {
            _ = synchronization.Release();
        }
    }

    private async Task UpdateAsync(IEnumerable<string> providerIds, bool enabled, bool remove, CancellationToken cancellationToken)
    {
        string[] ids = [.. providerIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase)];
        await synchronization.WaitAsync(cancellationToken);

        try
        {
            Dictionary<string, bool> previous;
            bool changed = false;

            lock (providers)
            {
                previous = new Dictionary<string, bool>(providers, StringComparer.OrdinalIgnoreCase);

                foreach (string id in ids)
                {
                    changed |= remove ? providers.Remove(id) : providers.TryAdd(id, enabled);
                }
            }

            if (changed)
            {
                await SaveOrRestoreAsync(previous, cancellationToken);
            }
        }
        finally
        {
            _ = synchronization.Release();
        }
    }

    private async Task SaveOrRestoreAsync(Dictionary<string, bool> previous, CancellationToken cancellationToken)
    {
        try
        {
            List<GlanceInspectorProviderPreference> snapshot;

            lock (providers)
            {
                snapshot = CreateSnapshot();
                settings.InspectorProviders = snapshot;
            }

            await writer.WriteAsync(value => value.InspectorProviders = [.. snapshot.Select(Clone)], cancellationToken);
        }
        catch
        {
            lock (providers)
            {
                providers.Clear();

                foreach ((string id, bool enabled) in previous)
                {
                    providers.Add(id, enabled);
                }

                settings.InspectorProviders = CreateSnapshot();
            }

            throw;
        }
    }

    private List<GlanceInspectorProviderPreference> CreateSnapshot() => [.. providers.OrderBy(provider => provider.Key, StringComparer.OrdinalIgnoreCase).Select(provider => new GlanceInspectorProviderPreference { Id = provider.Key, IsEnabled = provider.Value })];

    private static GlanceInspectorProviderPreference Clone(GlanceInspectorProviderPreference preference) => new() { Id = preference.Id, IsEnabled = preference.IsEnabled };
}
