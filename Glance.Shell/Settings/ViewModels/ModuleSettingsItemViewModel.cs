using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed partial class ModuleSettingsItemViewModel :
    ObservableObject,
    IModulesViewModel
{
    private readonly Action<ModuleSettingsItemViewModel> navigate;
    private readonly ITextLocalizer? localizer;
    private readonly Func<ModuleSettingsItemViewModel, Task<bool>>? uninstall;
    private bool suppressPersistence;

    public ModuleSettingsItemViewModel(string id,
        string displayName,
        string description,
        bool isEnabled,
        IEnumerable<IGlanceModuleSettingViewModel> settings,
        Action<ModuleSettingsItemViewModel> navigate,
        Func<ModuleSettingsItemViewModel, bool, Task<bool>> setEnabled) :
        this(id,
            displayName,
            description,
            null,
            isEnabled,
            settings,
            navigate,
            setEnabled,
            null)
    {
    }

    public ModuleSettingsItemViewModel(string id,
        string displayName,
        string description,
        IGlanceComponent? component,
        bool isEnabled,
        IEnumerable<IGlanceModuleSettingViewModel> settings,
        Action<ModuleSettingsItemViewModel> navigate,
        Func<ModuleSettingsItemViewModel, bool, Task<bool>> setEnabled,
        Func<ModuleSettingsItemViewModel, Task<bool>>? uninstall = null,
        ITextLocalizer? localizer = null)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        AccentResourceKey = component?.AccentResourceKey ?? "AccentTextFillColorPrimaryBrush";
        AccentResourceSource = component?.CompactContent;
        IconFontFamily = component?.IconFontFamily ?? "Segoe Fluent Icons";
        IconGlyph = string.IsNullOrEmpty(component?.IconGlyph) ? "\uE8B7" : component.IconGlyph;
        Settings = new ModuleSettingsViewModel(displayName, settings);
        this.navigate = navigate;
        this.uninstall = uninstall;
        this.localizer = localizer;
        this.isEnabled = isEnabled;
        SetEnabled = setEnabled;
        RefreshSettings();
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public string AccentResourceKey { get; }

    public object? AccentResourceSource { get; }

    public string IconFontFamily { get; }

    public string IconGlyph { get; }

    public bool HasSettings => Settings.HasSettings;

    public bool CanExpand => IsEnabled && HasSettings;

    public bool CanUninstall => uninstall is not null;

    public string UninstallDialogTitle => localizer?.GetText("UninstallModuleDialogTitle", DisplayName)
        ?? $"Uninstall {DisplayName}?";

    public string UninstallDialogMessage => localizer?.GetText("UninstallModuleDialogMessage", DisplayName)
        ?? $"{DisplayName} will be removed from Glance immediately.";

    public string UninstallDialogDataMessage => localizer?.GetText("UninstallModuleDialogDataMessage")
        ?? "Its package, settings, saved data, and runtime files will be deleted.";

    public string UninstallDialogPrimaryButtonText => localizer?.GetText("UninstallModuleDialogPrimaryButton")
        ?? "Uninstall";

    public string UninstallDialogCloseButtonText => localizer?.GetText("UninstallModuleDialogCloseButton")
        ?? "Cancel";

    public ModuleSettingsViewModel Settings { get; }

    private Func<ModuleSettingsItemViewModel, bool, Task<bool>> SetEnabled { get; }

    public void NavigateToSettings()
    {
        if (CanExpand)
        {
            navigate(this);
        }
    }

    public Task<bool> UninstallAsync() => uninstall?.Invoke(this) ?? Task.FromResult(false);

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
