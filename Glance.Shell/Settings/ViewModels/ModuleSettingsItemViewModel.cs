using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.Shell;

public partial class ModuleSettingsItemViewModel : ObservableObject
{
    private bool suppressPersistence;

    public ModuleSettingsItemViewModel(
        string id,
        string displayName,
        string description,
        bool isEnabled,
        Func<ModuleSettingsItemViewModel, bool, Task<bool>> setEnabled)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        this.isEnabled = isEnabled;
        SetEnabled = setEnabled;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Description { get; }

    private Func<ModuleSettingsItemViewModel, bool, Task<bool>> SetEnabled { get; }

    [ObservableProperty]
    private bool isEnabled;

    partial void OnIsEnabledChanged(bool value)
    {
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
}
