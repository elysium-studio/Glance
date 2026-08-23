using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed partial class ModuleSettingsItemViewModel :
    ObservableObject,
    IModulesViewModel
{
    private readonly IDispatcher dispatcher;
    private readonly Action<ModuleSettingsItemViewModel> navigate;
    private readonly IGlanceComponent? component;
    private readonly Func<ModuleSettingsItemViewModel, Task<bool>>? uninstall;
    private readonly Func<ModuleSettingsItemViewModel, Task<bool>>? install;
    private bool suppressPersistence;

    public ModuleSettingsItemViewModel(string id, string displayName, string description, IGlanceComponent? component, bool isEnabled, IEnumerable<IGlanceModuleSettingViewModel> settings, IDispatcher dispatcher, Action<ModuleSettingsItemViewModel> navigate, Func<ModuleSettingsItemViewModel, bool, Task<bool>> setEnabled, Func<ModuleSettingsItemViewModel, Task<bool>>? uninstall = null, Func<ModuleSettingsItemViewModel, Task<bool>>? install = null)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        AccentResourceKey = component?.AccentResourceKey ?? "AccentTextFillColorPrimaryBrush";
        AccentResourceSource = component?.CompactContent;
        IconFontFamily = component?.IconFontFamily ?? "Segoe Fluent Icons";
        IconGlyph = string.IsNullOrEmpty(component?.IconGlyph) ? "\uE8B7" : component.IconGlyph;
        Settings = new ModuleSettingsViewModel(displayName, settings);
        this.component = component;
        this.dispatcher = dispatcher;
        this.navigate = navigate;
        this.uninstall = uninstall;
        this.install = install;
        this.isEnabled = isEnabled;
        SetEnabled = setEnabled;
        PackageState = component is null ? ModulePackageState.Available : ModulePackageState.Installed;
        RefreshSettings();
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public string AccentResourceKey { get; }

    public object? AccentResourceSource { get; }

    public string IconFontFamily { get; private set; }

    public string IconGlyph { get; private set; }

    public GlanceModuleFeedItem? FeedItem { get; private set; }

    public GlanceModuleFeedIcon? FeedIcon => FeedItem?.Icon;

    public object? CreateIcon(bool isLightTheme) => component?.CreateIcon(isLightTheme);

    public bool HasSettings => Settings.HasSettings;

    public bool IsInstalled => component is not null;

    public bool CanExpand => IsInstalled && IsEnabled && HasSettings;

    public bool CanToggle => IsInstalled;

    public bool CanUninstall => IsInstalled && uninstall is not null;

    public bool ShowInstallAction => !IsInstalled && FeedItem is not null;

    public bool ShowUpdateAction => IsInstalled && FeedItem is not null && PackageState == ModulePackageState.UpdateAvailable;

    public bool CanInstall => ShowInstallAction && PackageState == ModulePackageState.Available && !IsBusy;

    public bool CanUpdate => ShowUpdateAction && !IsBusy;

    public ModuleSettingsViewModel Settings { get; }

    private Func<ModuleSettingsItemViewModel, bool, Task<bool>> SetEnabled { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExpand))]
    private bool isEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    [NotifyPropertyChangedFor(nameof(CanUpdate))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInstallAction))]
    [NotifyPropertyChangedFor(nameof(ShowUpdateAction))]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    [NotifyPropertyChangedFor(nameof(CanUpdate))]
    private ModulePackageState packageState;

    public void NavigateToSettings()
    {
        if (CanExpand)
        {
            navigate(this);
        }
    }

    public void SetFeedItem(GlanceModuleFeedItem feedItem, bool feedAvailable, string? installedVersion)
    {
        FeedItem = feedItem;
        IconFontFamily = string.IsNullOrWhiteSpace(feedItem.Icon.FontFamily) ? "Segoe Fluent Icons" : feedItem.Icon.FontFamily;
        IconGlyph = feedItem.Icon.Type == GlanceModuleIconType.Glyph ? feedItem.Icon.Source : "\uE8B7";
        PackageState = ResolvePackageState(feedItem, feedAvailable, installedVersion);
        OnPropertyChanged(nameof(FeedItem));
        OnPropertyChanged(nameof(FeedIcon));
        OnPropertyChanged(nameof(IconFontFamily));
        OnPropertyChanged(nameof(IconGlyph));
        OnPropertyChanged(nameof(ShowInstallAction));
        OnPropertyChanged(nameof(ShowUpdateAction));
        OnPropertyChanged(nameof(CanInstall));
        OnPropertyChanged(nameof(CanUpdate));
    }

    public async Task<bool> InstallAsync()
    {
        if ((!CanInstall && !CanUpdate) || install is null)
        {
            return false;
        }

        IsBusy = true;

        try
        {
            return await install(this);
        }
        finally
        {
            dispatcher.Dispatch(() => IsBusy = false);
        }
    }

    public Task<bool> UninstallAsync() => uninstall?.Invoke(this) ?? Task.FromResult(false);

    partial void OnIsEnabledChanged(bool value)
    {
        RefreshSettings();

        if (!suppressPersistence)
        {
            PersistEnabled(value);
        }
    }

    public void Dispose()
    {
        Settings.Dispose();
        GC.SuppressFinalize(this);
    }

    private static ModulePackageState ResolvePackageState(GlanceModuleFeedItem feedItem, bool feedAvailable, string? installedVersion)
    {
        if (!feedItem.IsCompatible)
        {
            return ModulePackageState.Incompatible;
        }

        if (installedVersion is not null)
        {
            return Version.TryParse(installedVersion, out Version? installed) && Version.TryParse(feedItem.Version, out Version? available) && available > installed ? ModulePackageState.UpdateAvailable : ModulePackageState.Installed;
        }

        return feedAvailable ? ModulePackageState.Available : ModulePackageState.Unavailable;
    }

    private async void PersistEnabled(bool value)
    {
        if (await SetEnabled(this, value))
        {
            return;
        }

        dispatcher.Dispatch(() =>
        {
            suppressPersistence = true;
            IsEnabled = !value;
            suppressPersistence = false;
        });
    }

    private void RefreshSettings() => Settings.SetEnabled(IsEnabled);
}
