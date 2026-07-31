using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed class ModulePreferenceService
{
    private readonly List<IGlanceComponent> allComponents;
    private readonly List<Func<IReadOnlyList<IGlanceModuleSettingViewModel>>> runtimeSettingsFactories = [];
    private readonly GlanceSettings settings;
    private readonly IWritableOptions<GlanceSettings> writer;

    public ModulePreferenceService(IEnumerable<IGlanceComponent> components, GlanceSettings settings, IWritableOptions<GlanceSettings> writer)
    {
        allComponents = [.. components.OrderBy(component => component.Order)];
        this.settings = settings;
        this.writer = writer;
        Normalize();
        TrackAvailability(allComponents);
    }

    public event EventHandler? ActiveComponentsChanged;

    public event EventHandler<GlanceComponentsAddedEventArgs>? ComponentsAdded;

    public event EventHandler? PreferencesChanged;

    public IReadOnlyList<IGlanceComponent> GetActiveComponents() =>
        [.. settings.Modules
            .Where(preference => preference.IsEnabled)
            .Select(preference => allComponents.FirstOrDefault(component =>
                string.Equals(component.Id, preference.Id, StringComparison.OrdinalIgnoreCase)))
            .OfType<IGlanceComponent>()
            .Where(IsAvailable)];

    public IReadOnlyList<GlanceModulePreference> GetPreferences() =>
        [.. settings.Modules
            .Where(preference => GetComponent(preference.Id) is not null)
            .Select(preference => new GlanceModulePreference
            {
                Id = preference.Id,
                IsAttentionEnabled = preference.IsAttentionEnabled,
                IsEnabled = preference.IsEnabled
            })];

    public IGlanceComponent? GetComponent(string id) =>
        allComponents.FirstOrDefault(component =>
            string.Equals(component.Id, id, StringComparison.OrdinalIgnoreCase));

    public bool IsEnabled(string id) =>
        settings.Modules.Any(preference => preference.IsEnabled && string.Equals(preference.Id, id, StringComparison.OrdinalIgnoreCase));

    public bool IsAttentionEnabled(string id)
    {
        IGlanceComponent? component = GetComponent(id);

        if (component is not IGlanceAttentionComponent attentionComponent)
        {
            return false;
        }

        GlanceModulePreference? preference = settings.Modules.FirstOrDefault(item =>
            string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));

        return preference?.IsAttentionEnabled ?? attentionComponent.IsAttentionEnabledByDefault;
    }

    public IReadOnlyList<IGlanceModuleSettingViewModel> CreateRuntimeSettings() =>
        [.. runtimeSettingsFactories.SelectMany(factory => factory()).OrderBy(setting => setting.Order)];

    public async Task RegisterComponentsAsync(IReadOnlyList<IGlanceComponent> components, Func<IReadOnlyList<IGlanceModuleSettingViewModel>> createSettings)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(createSettings);

        string[] ids = [.. components.Select(component => component.Id)];

        if (ids.Any(string.IsNullOrWhiteSpace) ||
            ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Length ||
            ids.Any(id => GetComponent(id) is not null))
        {
            throw new InvalidOperationException("A loaded module must provide unique, non-empty component identifiers.");
        }

        allComponents.AddRange(components);
        runtimeSettingsFactories.Add(createSettings);
        TrackAvailability(components);

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
            await writer.WriteAsync(value => value.Modules = [.. settings.Modules.Select(Clone)]);
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

    public async Task SetAttentionEnabledAsync(string id, bool isEnabled)
    {
        GlanceModulePreference? preference = settings.Modules.FirstOrDefault(item =>
            string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));

        if (preference is null || preference.IsAttentionEnabled == isEnabled)
        {
            return;
        }

        preference.IsAttentionEnabled = isEnabled;
        await SaveAsync();
    }

    public async Task SetOrderAsync(IEnumerable<string> orderedIds)
    {
        string[] ids = [.. orderedIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase)];
        Dictionary<string, GlanceModulePreference> preferences = settings.Modules.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        Queue<GlanceModulePreference> ordered = new(ids.Where(preferences.ContainsKey).Select(id => preferences[id]));
        HashSet<string> reorderedIds = [.. ordered.Select(item => item.Id)];
        List<GlanceModulePreference> modules = [.. settings.Modules.Select(item => reorderedIds.Contains(item.Id) ? ordered.Dequeue() : item)];

        if (modules.Select(item => item.Id).SequenceEqual(settings.Modules.Select(item => item.Id), StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        settings.Modules = modules;
        await SaveAsync();
    }

    private void Normalize()
    {
        Dictionary<string, GlanceModulePreference> saved = settings.Modules
            .Where(preference => !string.IsNullOrWhiteSpace(preference.Id))
            .GroupBy(preference => preference.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        settings.Modules = [.. settings.Modules
            .GroupBy(preference => preference.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())];

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
        List<GlanceModulePreference> snapshot = [.. settings.Modules.Select(Clone)];

        PreferencesChanged?.Invoke(this, EventArgs.Empty);
        await writer.WriteAsync(value => value.Modules = snapshot);
    }

    private static bool IsAvailable(IGlanceComponent component) =>
        component is not IGlanceAvailabilityComponent availability || availability.IsAvailable;

    private void TrackAvailability(IEnumerable<IGlanceComponent> components)
    {
        foreach (IGlanceAvailabilityComponent component in components.OfType<IGlanceAvailabilityComponent>())
        {
            component.AvailabilityChanged += HandleComponentAvailabilityChanged;
        }
    }

    private void HandleComponentAvailabilityChanged(object? sender, EventArgs args) =>
        ActiveComponentsChanged?.Invoke(this, EventArgs.Empty);

    private static GlanceModulePreference Clone(GlanceModulePreference preference) =>
        new()
        {
            Id = preference.Id,
            IsAttentionEnabled = preference.IsAttentionEnabled,
            IsEnabled = preference.IsEnabled
        };
}
