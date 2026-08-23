using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.Shell;

public sealed partial class ModuleFeedSettingItemViewModel :
    ObservableObject
{
    private readonly Func<ModuleFeedSettingItemViewModel, bool, Task> setEnabled;

    public ModuleFeedSettingItemViewModel(GlanceModuleFeedSource source, GlanceModuleFeedStatus? status, Func<ModuleFeedSettingItemViewModel, bool, Task> setEnabled)
    {
        Id = source.Id;
        DisplayName = source.DisplayName;
        Url = source.Uri.IsFile ? source.Uri.LocalPath : source.Uri.AbsoluteUri;
        IsBuiltIn = source.IsBuiltIn;
        IsAvailable = status?.IsAvailable == true;
        IsUsingCache = status?.IsUsingCache == true;
        ErrorMessage = status?.ErrorMessage ?? string.Empty;
        isEnabled = source.IsEnabled;
        this.setEnabled = setEnabled;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Url { get; }

    public bool IsBuiltIn { get; }

    public bool CanRemove => !IsBuiltIn;

    public bool IsAvailable { get; }

    public bool IsUsingCache { get; }

    public string ErrorMessage { get; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    [ObservableProperty]
    private bool isEnabled;

    partial void OnIsEnabledChanged(bool value) => _ = setEnabled(this, value);
}
