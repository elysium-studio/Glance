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
            .OrderBy(setting => setting.Order)
            .ToLookup(setting => setting.ModuleId, StringComparer.OrdinalIgnoreCase);
        Modules = new ObservableCollection<ModuleSettingsItemViewModel>(preferences.GetPreferences().Select(preference => CreateItem(preference, settingsByModule[preference.Id])));
    }

    public ObservableCollection<ModuleSettingsItemViewModel> Modules { get; }

    public Task SaveOrderAsync() =>
        preferences.SetOrderAsync(Modules.Select(item => item.Id));

    public override void Dispose()
    {
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
}
