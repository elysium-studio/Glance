using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed class ModulePreferenceService
{
    private readonly List<IGlanceComponent> allComponents;
    private readonly List<Func<IReadOnlyList<IGlanceModuleSettingViewModel>>> runtimeSettingsFactories = [];
    private readonly GlanceSettings settings;
    private readonly IWritableOptions<GlanceSettings> writer;

    public ModulePreferenceService(
        IEnumerable<IGlanceComponent> components,
        GlanceSettings settings,
        IWritableOptions<GlanceSettings> writer)
    {
        allComponents = components.OrderBy(component => component.Order).ToList();
        this.settings = settings;
        this.writer = writer;
        Normalize();
    }

    public event EventHandler<GlanceComponentsAddedEventArgs>? ComponentsAdded;

    public event EventHandler? PreferencesChanged;

    public IReadOnlyList<IGlanceComponent> GetActiveComponents() =>
        settings.Modules
            .Where(preference => preference.IsEnabled)
            .Select(preference => allComponents.FirstOrDefault(component =>
                string.Equals(component.Id, preference.Id, StringComparison.OrdinalIgnoreCase)))
            .OfType<IGlanceComponent>().ToArray();

    public IReadOnlyList<GlanceModulePreference> GetPreferences() =>
        settings.Modules
            .Where(preference => GetComponent(preference.Id) is not null)
            .Select(preference => new GlanceModulePreference
            {
                Id = preference.Id,
                IsEnabled = preference.IsEnabled
            })
            .ToArray();

    public IGlanceComponent? GetComponent(string id) =>
        allComponents.FirstOrDefault(component =>
            string.Equals(component.Id, id, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<IGlanceModuleSettingViewModel> CreateRuntimeSettings() =>
        runtimeSettingsFactories.SelectMany(factory => factory()).OrderBy(setting => setting.Order).ToArray();

    public async Task RegisterComponentsAsync(
        IReadOnlyList<IGlanceComponent> components,
        Func<IReadOnlyList<IGlanceModuleSettingViewModel>> createSettings)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(createSettings);

        string[] ids = components.Select(component => component.Id).ToArray();

        if (ids.Any(string.IsNullOrWhiteSpace) ||
            ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Length ||
            ids.Any(id => GetComponent(id) is not null))
        {
            throw new InvalidOperationException("A loaded module must provide unique, non-empty component identifiers.");
        }

        allComponents.AddRange(components);
        runtimeSettingsFactories.Add(createSettings);

        bool settingsChanged = false;

        foreach (IGlanceComponent component in components)
        {
            if (settings.Modules.Any(preference => string.Equals(preference.Id, component.Id, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            settings.Modules.Add(new GlanceModulePreference { Id = component.Id });
            settingsChanged = true;
        }

        PreferencesChanged?.Invoke(this, EventArgs.Empty);
        ComponentsAdded?.Invoke(this, new GlanceComponentsAddedEventArgs(components, createSettings));

        if (settingsChanged)
        {
            await writer.WriteAsync(value => value.Modules = settings.Modules.Select(Clone).ToList());
        }
    }

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
            .Where(item => GetComponent(item.Id) is not null)
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
        ordered.AddRange(settings.Modules.Where(item => GetComponent(item.Id) is null));
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
        List<GlanceModulePreference> snapshot = settings.Modules.Select(Clone).ToList();

        PreferencesChanged?.Invoke(this, EventArgs.Empty);
        await writer.WriteAsync(value => value.Modules = snapshot);
    }

    private static GlanceModulePreference Clone(GlanceModulePreference preference) =>
        new()
        {
            Id = preference.Id,
            IsEnabled = preference.IsEnabled
        };
}
