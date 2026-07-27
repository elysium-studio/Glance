using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed partial class ModuleSettingsItemViewModel :
    ObservableObject,
    IModulesViewModel
{
    private readonly Action<ModuleSettingsItemViewModel> navigate;
    private readonly Action requestReordering;
    private bool suppressPersistence;

    public ModuleSettingsItemViewModel(string id,
        string displayName,
        string description,
        bool isEnabled,
        IEnumerable<IGlanceModuleSettingViewModel> settings,
        Action<ModuleSettingsItemViewModel> navigate,
        Action requestReordering,
        Func<ModuleSettingsItemViewModel, bool, Task<bool>> setEnabled)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        Settings = new ModuleSettingsViewModel(settings);
        this.navigate = navigate;
        this.requestReordering = requestReordering;
        this.isEnabled = isEnabled;
        SetEnabled = setEnabled;
        RefreshSettings();
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public bool HasSettings => Settings.HasSettings;

    public bool CanExpand => IsEnabled && HasSettings;

    public bool CanInteract => !IsReordering;

    public bool CanNavigate => CanExpand && CanInteract;

    public ModuleSettingsViewModel Settings { get; }

    private Func<ModuleSettingsItemViewModel, bool, Task<bool>> SetEnabled { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteract))]
    [NotifyPropertyChangedFor(nameof(CanNavigate))]
    private bool isReordering;

    public void NavigateToSettings()
    {
        if (CanNavigate)
        {
            navigate(this);
        }
    }

    public void RequestReordering()
    {
        if (!IsReordering)
        {
            requestReordering();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExpand))]
    [NotifyPropertyChangedFor(nameof(CanNavigate))]
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
