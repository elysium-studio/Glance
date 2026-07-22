using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;

namespace Glance.Shell;

public partial class ModuleSettingsItemViewModel :
    ObservableObject,
    IDisposable
{
    private readonly IReadOnlyList<IGlanceModuleSettingViewModel> availableSettings;
    private bool suppressPersistence;

    public ModuleSettingsItemViewModel(
        string id,
        string displayName,
        string description,
        bool isEnabled,
        IEnumerable<IGlanceModuleSettingViewModel> settings,
        Func<ModuleSettingsItemViewModel, bool, Task<bool>> setEnabled)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        availableSettings = settings.ToArray();
        this.isEnabled = isEnabled;
        SetEnabled = setEnabled;
        RefreshSettings();
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public bool HasSettings => availableSettings.Count > 0;

    public ObservableCollection<IGlanceModuleSettingViewModel> Settings { get; } = [];

    private Func<ModuleSettingsItemViewModel, bool, Task<bool>> SetEnabled { get; }

    [ObservableProperty]
    private bool isEnabled;

    [ObservableProperty]
    private bool isExpanded;

    partial void OnIsEnabledChanged(bool value)
    {
        RefreshSettings();

        if (!suppressPersistence)
        {
            PersistEnabled(value);
        }
    }

    private async void PersistEnabled(bool value)
    {
        if (await SetEnabled(this, value))
        {
            return;
        }

        suppressPersistence = true;
        IsEnabled = !value;
        suppressPersistence = false;
    }

    public void Dispose()
    {
        Settings.Clear();

        foreach (IGlanceModuleSettingViewModel setting in availableSettings)
        {
            setting.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private void RefreshSettings()
    {
        IsExpanded = IsEnabled && IsExpanded;
        Settings.Clear();

        if (!IsEnabled)
        {
            return;
        }

        foreach (IGlanceModuleSettingViewModel setting in availableSettings)
        {
            Settings.Add(setting);
        }
    }
}
