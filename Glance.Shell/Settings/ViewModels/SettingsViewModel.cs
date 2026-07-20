using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;

namespace Glance.Shell;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ModulePreferenceService preferences;

    public SettingsViewModel(ModulePreferenceService preferences)
    {
        this.preferences = preferences;

        Modules = new ObservableCollection<ModuleSettingsItemViewModel>(
            preferences.GetPreferences().Select(CreateItem));
    }

    public ObservableCollection<ModuleSettingsItemViewModel> Modules { get; }

    public Task SaveOrderAsync() =>
        preferences.SetOrderAsync(Modules.Select(item => item.Id));

    private ModuleSettingsItemViewModel CreateItem(GlanceModulePreference preference)
    {
        IGlanceComponent? component = preferences.GetComponent(preference.Id);
        string displayName = component?.DisplayName ?? preference.Id;
        string description = component?.Description ?? string.Empty;

        return new ModuleSettingsItemViewModel(
            preference.Id,
            displayName,
            description,
            preference.IsEnabled,
            (_, enabled) => preferences.SetEnabledAsync(preference.Id, enabled));
    }
}
