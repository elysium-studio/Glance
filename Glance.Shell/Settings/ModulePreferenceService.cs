using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed class ModulePreferenceService
{
    private readonly IReadOnlyList<IGlanceComponent> allComponents;
    private readonly GlanceSettings settings;
    private readonly IWritableOptions<GlanceSettings> writer;

    public ModulePreferenceService(
        IEnumerable<IGlanceComponent> components,
        GlanceSettings settings,
        IWritableOptions<GlanceSettings> writer)
    {
        allComponents = components.OrderBy(component => component.Order).ToArray();
        this.settings = settings;
        this.writer = writer;
        Normalize();
    }

    public event EventHandler? PreferencesChanged;

    public IReadOnlyList<IGlanceComponent> GetActiveComponents() =>
        settings.Modules
            .Where(preference => preference.IsEnabled)
            .Select(preference => allComponents.FirstOrDefault(component =>
                string.Equals(component.Id, preference.Id, StringComparison.OrdinalIgnoreCase)))
            .OfType<IGlanceComponent>().ToArray();

    public IReadOnlyList<GlanceModulePreference> GetPreferences() =>
        settings.Modules
            .Select(preference => new GlanceModulePreference
            {
                Id = preference.Id,
                IsEnabled = preference.IsEnabled
            })
            .ToArray();

    public IGlanceComponent? GetComponent(string id) =>
        allComponents.FirstOrDefault(component =>
            string.Equals(component.Id, id, StringComparison.OrdinalIgnoreCase));

    public async Task<bool> SetEnabledAsync(string id, bool isEnabled)
    {
        GlanceModulePreference? preference = settings.Modules.FirstOrDefault(item =>
            string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));

        if (preference is null || preference.IsEnabled == isEnabled)
        {
            return true;
        }

        if (!isEnabled && settings.Modules.Count(item => item.IsEnabled) <= 1)
        {
            return false;
        }

        preference.IsEnabled = isEnabled;
        await SaveAsync();
        return true;
    }

    public async Task SetOrderAsync(IEnumerable<string> orderedIds)
    {
        Dictionary<string, GlanceModulePreference> preferences = settings.Modules
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        List<GlanceModulePreference> ordered = [];

        foreach (string id in orderedIds)
        {
            if (preferences.Remove(id, out GlanceModulePreference? preference))
            {
                ordered.Add(preference);
            }
        }

        ordered.AddRange(preferences.Values);
        settings.Modules = ordered;
        await SaveAsync();
    }

    private void Normalize()
    {
        Dictionary<string, GlanceModulePreference> saved = settings.Modules
            .Where(preference => !string.IsNullOrWhiteSpace(preference.Id))
            .GroupBy(preference => preference.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        settings.Modules = settings.Modules
            .Where(preference => allComponents.Any(component =>
                string.Equals(component.Id, preference.Id, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(preference => preference.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        foreach (IGlanceComponent component in allComponents)
        {
            if (!saved.ContainsKey(component.Id))
            {
                settings.Modules.Add(new GlanceModulePreference { Id = component.Id });
            }
        }
    }

    private async Task SaveAsync()
    {
        List<GlanceModulePreference> snapshot = GetPreferences().ToList();

        PreferencesChanged?.Invoke(this, EventArgs.Empty);
        await writer.WriteAsync(value => value.Modules = snapshot);
    }
}
