namespace Glance.Shell;

public sealed class GlanceModuleFeedSourceProvider :
    IGlanceModuleFeedSourceProvider
{
    private readonly IReadOnlyList<GlanceModuleFeedDefinition> definitions;
    private readonly GlanceSettings settings;

    public GlanceModuleFeedSourceProvider(GlanceSettings settings, IEnumerable<GlanceModuleFeedDefinition> definitions)
    {
        this.settings = settings;
        this.definitions = [.. definitions.OrderBy(definition => definition.Priority)];
    }

    public IReadOnlyList<GlanceModuleFeedSource> GetSources()
    {
        Dictionary<string, GlanceModuleFeedPreference> preferences = settings.ModuleFeeds.GroupBy(preference => preference.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        List<GlanceModuleFeedSource> sources = [];

        foreach (GlanceModuleFeedDefinition definition in definitions)
        {
            GlanceModuleFeedPreference? preference = preferences.GetValueOrDefault(definition.Id);
            sources.Add(new GlanceModuleFeedSource(definition.Id, definition.DisplayName, definition.Uri, preference?.IsEnabled ?? definition.IsEnabled, true, definition.AllowLocalPackages, definition.Priority));
        }

        sources.AddRange(settings.ModuleFeeds.Where(preference => !preference.IsBuiltIn && Uri.TryCreate(preference.Url, UriKind.Absolute, out _)).Select((preference, index) => new GlanceModuleFeedSource(preference.Id, preference.DisplayName, new Uri(preference.Url), preference.IsEnabled, false, false, 1000 + index)));
        return sources;
    }
}
