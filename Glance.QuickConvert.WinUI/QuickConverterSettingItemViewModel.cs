using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.QuickConvert.WinUI;

public sealed partial class QuickConverterSettingItemViewModel :
    ObservableObject
{
    private readonly Func<QuickConverterSettingItemViewModel, bool, Task<bool>> setEnabled;
    private bool suppressPersistence;

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    public QuickConverterSettingItemViewModel(string id, string displayName, string description, bool isEnabled, bool canRemove, Func<QuickConverterSettingItemViewModel, bool, Task<bool>> setEnabled)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        CanRemove = canRemove;
        this.setEnabled = setEnabled;
        suppressPersistence = true;
        IsEnabled = isEnabled;
        suppressPersistence = false;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public bool CanRemove { get; }

    public void SynchronizeEnabled(bool enabled)
    {
        suppressPersistence = true;
        IsEnabled = enabled;
        suppressPersistence = false;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        if (!suppressPersistence)
        {
            PersistEnabled(value);
        }
    }

    private async void PersistEnabled(bool value)
    {
        if (await setEnabled(this, value))
        {
            return;
        }

        suppressPersistence = true;
        IsEnabled = !value;
        suppressPersistence = false;
    }
}
