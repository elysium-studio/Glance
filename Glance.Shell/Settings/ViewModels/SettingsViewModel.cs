using CommunityToolkit.Mvvm.ComponentModel;
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
        (string displayName, string description) = preference.Id switch
        {
            "Stopwatch" => ("Stopwatch", "Track elapsed time with start and reset controls."),
            "Timer" => ("Timer", "Run a countdown and receive attention when it completes."),
            "Media" => ("Media playback", "See the current track and control system media playback."),
            "SystemMonitor" => ("System monitor", "See live processor and memory usage."),
            "Power" => ("Battery and power", "See charge, power source, and estimated battery time."),
            _ => (preference.Id, "Glance module")
        };

        return new ModuleSettingsItemViewModel(
            preference.Id,
            displayName,
            description,
            preference.IsEnabled,
            (_, enabled) => preferences.SetEnabledAsync(preference.Id, enabled));
    }
}
