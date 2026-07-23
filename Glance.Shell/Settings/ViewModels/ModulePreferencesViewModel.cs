using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;

namespace Glance.Shell;

public sealed partial class ModulePreferencesViewModel :
    ObservableViewModel,
    IModulesViewModel
{
    private readonly ModulePreferenceService preferences;

    public ModulePreferencesViewModel(
        IServiceProvider provider,
        IServiceFactory factory,
        IMessenger messenger,
        IDisposer disposer,
        ModulePreferenceService preferences,
        IEnumerable<IGlanceModuleSettingViewModel> settings) :
        base(provider, factory, messenger, disposer)
    {
        this.preferences = preferences;
        ILookup<string, IGlanceModuleSettingViewModel> settingsByModule = settings
            .Concat(preferences.CreateRuntimeSettings())
            .OrderBy(setting => setting.Order)
            .ToLookup(setting => setting.ModuleId, StringComparer.OrdinalIgnoreCase);
        Modules = new ObservableCollection<ModuleSettingsItemViewModel>(preferences.GetPreferences().Select(preference => CreateItem(preference, settingsByModule[preference.Id])));
        preferences.ComponentsAdded += HandleComponentsAdded;
    }

    public ObservableCollection<ModuleSettingsItemViewModel> Modules { get; }

    public Task SaveOrderAsync() =>
        preferences.SetOrderAsync(Modules.Select(item => item.Id));

    public override void Dispose()
    {
        preferences.ComponentsAdded -= HandleComponentsAdded;

        foreach (ModuleSettingsItemViewModel module in Modules)
        {
            module.Dispose();
        }

        Modules.Clear();
        base.Dispose();
    }

    private ModuleSettingsItemViewModel CreateItem(GlanceModulePreference preference, IEnumerable<IGlanceModuleSettingViewModel> settings)
    {
        IGlanceComponent? component = preferences.GetComponent(preference.Id);
        string displayName = component?.DisplayName ?? preference.Id;
        string description = component?.Description ?? string.Empty;

        return new ModuleSettingsItemViewModel(
            preference.Id,
            displayName,
            description,
            preference.IsEnabled,
            settings,
            (_, enabled) => preferences.SetEnabledAsync(preference.Id, enabled));
    }

    private void HandleComponentsAdded(object? sender, GlanceComponentsAddedEventArgs args)
    {
        ILookup<string, IGlanceModuleSettingViewModel> settingsByModule = args.CreateSettings()
            .OrderBy(setting => setting.Order)
            .ToLookup(setting => setting.ModuleId, StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<GlanceModulePreference> orderedPreferences = preferences.GetPreferences();

        foreach (IGlanceComponent component in args.Components)
        {
            GlanceModulePreference preference = orderedPreferences.First(item => string.Equals(item.Id, component.Id, StringComparison.OrdinalIgnoreCase));
            int index = orderedPreferences.Select(item => item.Id).TakeWhile(id => !string.Equals(id, component.Id, StringComparison.OrdinalIgnoreCase)).Count();
            Modules.Insert(Math.Min(index, Modules.Count), CreateItem(preference, settingsByModule[component.Id]));
        }
    }
}
