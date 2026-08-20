using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed class GlanceInspectorProviderRegistry :
    IGlanceInspectorProviderRegistry
{
    private readonly Dictionary<string, GlanceInspectorProviderRegistration> providers = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly IGlanceInspectorProviderPreferences preferences;
    private readonly object synchronization = new();

    public GlanceInspectorProviderRegistry(IGlanceInspectorProviderPreferences preferences) => this.preferences = preferences;

    internal event EventHandler? Changed;

    public IReadOnlyList<IGlanceInspectorProvider> GetProviders(GlanceContentContext context)
    {
        lock (synchronization)
        {
            return [.. providers.Values.Where(registration => preferences.IsEnabled(registration.Provider.Descriptor.Id)).Select(registration => (registration.Provider, Match: registration.Provider.Match(context))).Where(item => item.Match != GlanceInspectorMatch.None).OrderByDescending(item => item.Match).ThenBy(item => item.Provider.Descriptor.DisplayName).Select(item => item.Provider)];
        }
    }

    internal IReadOnlyList<GlanceInspectorProviderRegistration> GetRegistrations()
    {
        lock (synchronization)
        {
            return [.. providers.Values.OrderBy(registration => registration.Provider.Descriptor.DisplayName)];
        }
    }

    public void Register(string? packageId, IEnumerable<IGlanceInspectorProvider> registrations)
    {
        IGlanceInspectorProvider[] additions = [.. registrations];

        lock (synchronization)
        {
            string? duplicateId = additions.Select(provider => provider.Descriptor.Id).GroupBy(id => id, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1 || providers.ContainsKey(group.Key)).Select(group => group.Key).FirstOrDefault();

            if (duplicateId is not null)
            {
                throw new InvalidOperationException($"An inspector provider with the identifier '{duplicateId}' is already registered.");
            }

            foreach (IGlanceInspectorProvider provider in additions)
            {
                providers.Add(provider.Descriptor.Id, new GlanceInspectorProviderRegistration(provider, packageId));
            }
        }

        if (additions.Length > 0)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Unregister(IEnumerable<IGlanceInspectorProvider> registrations)
    {
        HashSet<IGlanceInspectorProvider> removals = [.. registrations];
        bool changed;

        lock (synchronization)
        {
            string[] ids = [.. providers.Where(item => removals.Contains(item.Value.Provider)).Select(item => item.Key)];

            foreach (string id in ids)
            {
                _ = providers.Remove(id);
            }

            changed = ids.Length > 0;
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
