using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed partial class ModuleSettingsItemViewModel :
    ObservableObject,
    IModulesViewModel
{
    private readonly Action<ModuleSettingsItemViewModel> navigate;
    private bool suppressPersistence;

    public ModuleSettingsItemViewModel(string id,
        string displayName,
        string description,
        bool isEnabled,
        IEnumerable<IGlanceModuleSettingViewModel> settings,
        Action<ModuleSettingsItemViewModel> navigate,
        Func<ModuleSettingsItemViewModel, bool, Task<bool>> setEnabled)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        Settings = new ModuleSettingsViewModel(displayName, settings);
        this.navigate = navigate;
        this.isEnabled = isEnabled;
        SetEnabled = setEnabled;
        RefreshSettings();
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public bool HasSettings => Settings.HasSettings;

    public bool CanExpand => IsEnabled && HasSettings;

    public ModuleSettingsViewModel Settings { get; }

    private Func<ModuleSettingsItemViewModel, bool, Task<bool>> SetEnabled { get; }

    public void NavigateToSettings()
    {
        if (CanExpand)
        {
            navigate(this);
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExpand))]
    private bool isEnabled;

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
        Settings.Dispose();
        GC.SuppressFinalize(this);
    }

    private void RefreshSettings() => Settings.SetEnabled(IsEnabled);
}
