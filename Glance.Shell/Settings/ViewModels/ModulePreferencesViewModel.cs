using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;

namespace Glance.Shell;

public partial class ModulePreferencesViewModel :
    ObservableViewModel,
    IModulesViewModel
{
    private readonly ModulePreferenceService preferences;

    public ModulePreferencesViewModel(
        IServiceProvider provider,
        IServiceFactory factory,
        IMessenger messenger,
        IDisposer disposer,
        ModulePreferenceService preferences) :
        base(provider, factory, messenger, disposer)
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
