using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed class GlanceQuickConverterRegistry :
    IGlanceQuickConverterRegistry
{
    private readonly Dictionary<string, GlanceQuickConverterRegistration> converters = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly IGlanceQuickConverterPreferences preferences;
    private readonly object synchronization = new();

    internal event EventHandler? Changed;

    public GlanceQuickConverterRegistry(IGlanceQuickConverterPreferences preferences) => this.preferences = preferences;

    public IReadOnlyList<IGlanceQuickConverter> GetConverters(GlanceContentContext context)
    {
        lock (synchronization)
        {
            return
            [
                .. converters.Values
                    .Where(registration => preferences.IsEnabled(registration.Converter.Descriptor.Id))
                    .Select(registration => (converter: registration.Converter, match: registration.Converter.Match(context)))
                    .Where(item => item.match != GlanceQuickConverterMatch.None)
                    .OrderByDescending(item => item.match)
                    .ThenBy(item => item.converter.Descriptor.DisplayName)
                    .Select(item => item.converter)
            ];
        }
    }

    internal IReadOnlyList<GlanceQuickConverterRegistration> GetRegistrations()
    {
        lock (synchronization)
        {
            return [.. converters.Values.OrderBy(registration => registration.Converter.Descriptor.DisplayName)];
        }
    }

    public void Register(IEnumerable<IGlanceQuickConverter> registrations) => Register(null, registrations);

    public void Register(string? packageId, IEnumerable<IGlanceQuickConverter> registrations)
    {
        IGlanceQuickConverter[] additions = [.. registrations];

        lock (synchronization)
        {
            string? duplicateId = additions
                .Select(converter => converter.Descriptor.Id)
                .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1 || converters.ContainsKey(group.Key))
                .Select(group => group.Key)
                .FirstOrDefault();

            if (duplicateId is not null)
            {
                throw new InvalidOperationException($"A quick converter with the identifier '{duplicateId}' is already registered.");
            }

            foreach (IGlanceQuickConverter converter in additions)
            {
                converters.Add(converter.Descriptor.Id, new GlanceQuickConverterRegistration(converter, packageId));
            }
        }

        if (additions.Length > 0)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Unregister(IEnumerable<IGlanceQuickConverter> registrations)
    {
        HashSet<IGlanceQuickConverter> removals = [.. registrations];
        bool changed;

        lock (synchronization)
        {
            string[] ids = [.. converters.Where(item => removals.Contains(item.Value.Converter)).Select(item => item.Key)];

            foreach (string id in ids)
            {
                _ = converters.Remove(id);
            }

            changed = ids.Length > 0;
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
