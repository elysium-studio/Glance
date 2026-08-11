using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed partial class ModulesViewModel :
    ObservableCollectionViewModel<ISettingViewModel>,
    ISettingViewModel
{
    private static readonly string[] CategoryOrder =
    [
        GlanceModuleCategories.Information,
        GlanceModuleCategories.Productivity,
        GlanceModuleCategories.MediaAndCapture,
        GlanceModuleCategories.DevicesAndSystem,
        GlanceModuleCategories.Integrations,
        GlanceModuleCategories.Other
    ];
    private readonly Dictionary<string, ModuleSettingsCategoryViewModel> categories = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly IApplicationRestartService applicationRestart;
    private readonly IDispatcher dispatcher;
    private readonly ITextLocalizer localizer;
    private readonly ModulePreferenceService preferences;
    private readonly ModuleInstallationService installations;

    public ModulesViewModel(IServiceProvider provider,
        IServiceFactory factory,
        IMessenger messenger,
        IDisposer disposer,
        IDispatcher dispatcher,
        ModulePreferenceService preferences,
        ModuleInstallationService installations,
        IApplicationRestartService applicationRestart,
        ITextLocalizer localizer,
        IEnumerable<IGlanceModuleSettingViewModel> settings) :
        base(provider, factory, messenger, disposer)
    {
        this.dispatcher = dispatcher;
        this.preferences = preferences;
        this.installations = installations;
        this.applicationRestart = applicationRestart;
        this.localizer = localizer;
        Title = localizer.GetText("ModulesSectionTitle/Text");
        ILookup<string, IGlanceModuleSettingViewModel> settingsByModule = settings
            .Concat(preferences.CreateRuntimeSettings())
            .OrderBy(setting => setting.Order)
            .ToLookup(setting => setting.ModuleId, StringComparer.OrdinalIgnoreCase);

        foreach (GlanceModulePreference preference in preferences.GetPreferences())
        {
            IGlanceComponent? component = preferences.GetComponent(preference.Id);
            ModuleSettingsCategoryViewModel category = GetOrCreateCategory(component?.SettingsCategory ?? GlanceModuleCategories.Other);
            category.Add(CreateItem(preference, settingsByModule[preference.Id]));
        }

        preferences.ComponentsAdded += HandleComponentsAdded;
        preferences.ComponentsRemoved += HandleComponentsRemoved;
    }

    public IReadOnlyList<ISettingViewModel> Children => [.. this];

    public string Glyph => "\uE74C";

    public string Title { get; }

    public ModuleSettingsCategoryViewModel? FindCategoryForComponent(string componentId) => categories.Values
        .FirstOrDefault(category => category.OfType<ModuleSettingsItemViewModel>()
            .Any(item => string.Equals(item.Id, componentId, StringComparison.OrdinalIgnoreCase)));

    public string? FindDisplayNameForComponent(string componentId) => categories.Values
        .SelectMany(category => category.OfType<ModuleSettingsItemViewModel>())
        .FirstOrDefault(item => string.Equals(item.Id, componentId, StringComparison.OrdinalIgnoreCase))
        ?.DisplayName;

    public bool CanInstall => !IsInstalling;

    public string RestartDialogTitle => localizer.GetText("ModuleUpdateRestartDialogTitle");

    public string RestartDialogMessage => localizer.GetText("ModuleUpdateRestartDialogMessage");

    public string RestartDialogPrimaryButtonText => localizer.GetText("ModuleUpdateRestartDialogPrimaryButton");

    public string RestartDialogCloseButtonText => localizer.GetText("ModuleUpdateRestartDialogCloseButton");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    private bool isInstalling;

    [ObservableProperty]
    private bool isInstallStatusOpen;

    [ObservableProperty]
    private string installStatusMessage = string.Empty;

    [ObservableProperty]
    private ModuleInstallStatusKind installStatusKind;

    public async Task<ModuleInstallResult?> InstallAsync(IEnumerable<string> paths)
    {
        SetInstalling(true);
        ModuleInstallResult? lastResult = null;
        bool restartRequired = false;

        try
        {
            foreach (string path in paths)
            {
                ModuleInstallResult result = await installations.InstallAsync(path);

                if (!result.IsSuccessful)
                {
                    ShowInstallStatus(ModuleInstallStatusKind.Error,
                        string.IsNullOrWhiteSpace(result.ErrorMessage)
                            ? localizer.GetText("ModuleInstallFailedMessage")
                            : result.ErrorMessage);
                    return null;
                }

                lastResult = result;
                restartRequired |= result.RequiresRestart;
            }

            if (lastResult is null)
            {
                return null;
            }

            ModuleInstallResult completedResult = lastResult with { RequiresRestart = restartRequired };
            string installedModuleNames = ResolveInstalledModuleNames(completedResult);
            ModuleSettingsCategoryViewModel? category = completedResult.ComponentIds
                .Select(FindCategoryForComponent)
                .FirstOrDefault(candidate => candidate is not null);

            if (category is not null)
            {
                dispatcher.Dispatch(() => _ = Messenger.Send(new SettingsNavigationRequestedEventArgs(this, category)));
            }

            ShowInstallStatus(ModuleInstallStatusKind.Success,
                restartRequired
                    ? localizer.GetText("ModuleUpdateStagedMessage", installedModuleNames)
                    : localizer.GetText("ModuleInstalledMessage", installedModuleNames));
            return completedResult;
        }
        catch (Exception exception)
        {
            ShowInstallStatus(ModuleInstallStatusKind.Error,
                string.IsNullOrWhiteSpace(exception.Message)
                    ? localizer.GetText("ModuleInstallFailedMessage")
                    : exception.Message);
            return null;
        }
        finally
        {
            SetInstalling(false);
        }
    }

    public Task RestartAsync() => applicationRestart.RestartAsync();

    public void ShowInvalidPackageStatus() => ShowInstallStatus(ModuleInstallStatusKind.Error,
        localizer.GetText("ModuleInstallInvalidPackageMessage"));

    public void ShowInstallFailure(string message) => ShowInstallStatus(ModuleInstallStatusKind.Error,
        string.IsNullOrWhiteSpace(message) ? localizer.GetText("ModuleInstallFailedMessage") : message);

    public override void Dispose()
    {
        preferences.ComponentsAdded -= HandleComponentsAdded;
        preferences.ComponentsRemoved -= HandleComponentsRemoved;
        base.Dispose();
    }

    private ModuleSettingsItemViewModel CreateItem(GlanceModulePreference preference,
        IEnumerable<IGlanceModuleSettingViewModel> settings)
    {
        IGlanceComponent? component = preferences.GetComponent(preference.Id);
        string displayName = component?.DisplayName ?? preference.Id;
        string description = component?.Description ?? string.Empty;
        List<IGlanceModuleSettingViewModel> availableSettings = [.. settings];

        if (component is IGlanceAttentionComponent)
        {
            availableSettings.Add(new ModuleAttentionSettingViewModel(preference.Id,
                preferences.IsAttentionEnabled(preference.Id),
                enabled => preferences.SetAttentionEnabledAsync(preference.Id, enabled)));
        }

        return new ModuleSettingsItemViewModel(preference.Id,
            displayName,
            description,
            component,
            preference.IsEnabled,
            availableSettings.OrderBy(setting => setting.Order),
            NavigateToModule,
            (_, enabled) => preferences.SetEnabledAsync(preference.Id, enabled),
            installations.CanUninstall(preference.Id)
                ? _ => installations.UninstallAsync(preference.Id)
                : null);
    }

    private ModuleSettingsCategoryViewModel GetOrCreateCategory(string id)
    {
        if (categories.TryGetValue(id, out ModuleSettingsCategoryViewModel? category))
        {
            return category;
        }

        category = new ModuleSettingsCategoryViewModel(id,
            ResolveCategoryTitle(id),
            ResolveCategoryGlyph(id),
            [],
            this);
        categories.Add(id, category);
        int categoryOrder = GetCategoryOrder(id);
        int index = this.TakeWhile(item => item is ModuleSettingsCategoryViewModel existing && GetCategoryOrder(existing.Id) <= categoryOrder).Count();
        Insert(index, category);
        return category;
    }

    private int GetCategoryOrder(string id)
    {
        int index = Array.FindIndex(CategoryOrder, category => string.Equals(category, id, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? CategoryOrder.Length : index;
    }

    private void HandleComponentsAdded(object? sender,
        GlanceComponentsAddedEventArgs args)
    {
        ILookup<string, IGlanceModuleSettingViewModel> settingsByModule = args.CreateSettings()
            .OrderBy(setting => setting.Order)
            .ToLookup(setting => setting.ModuleId, StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<GlanceModulePreference> orderedPreferences = preferences.GetPreferences();

        foreach (IGlanceComponent component in args.Components)
        {
            GlanceModulePreference preference = orderedPreferences.First(item => string.Equals(item.Id, component.Id, StringComparison.OrdinalIgnoreCase));
            ModuleSettingsCategoryViewModel category = GetOrCreateCategory(component.SettingsCategory);
            int index = orderedPreferences
                .TakeWhile(item => !string.Equals(item.Id, component.Id, StringComparison.OrdinalIgnoreCase))
                .Count(item => string.Equals(preferences.GetComponent(item.Id)?.SettingsCategory, component.SettingsCategory, StringComparison.OrdinalIgnoreCase));
            category.Insert(Math.Min(index, category.Count), CreateItem(preference, settingsByModule[component.Id]));
        }
    }

    private void HandleComponentsRemoved(object? sender,
        GlanceComponentsRemovedEventArgs args)
    {
        HashSet<string> ids = [with(StringComparer.OrdinalIgnoreCase), .. args.Components.Select(component => component.Id)];
        string[] displayNames = [.. categories.Values
            .SelectMany(category => category.OfType<ModuleSettingsItemViewModel>())
            .Where(item => ids.Contains(item.Id))
            .Select(item => item.DisplayName)];

        foreach (ModuleSettingsCategoryViewModel category in categories.Values.ToArray())
        {
            foreach (ModuleSettingsItemViewModel item in category.OfType<ModuleSettingsItemViewModel>().Where(item => ids.Contains(item.Id)).ToArray())
            {
                _ = category.Remove(item);
                item.Dispose();
            }

            if (category.Count == 0)
            {
                _ = Remove(category);
                _ = categories.Remove(category.Id);
            }
        }

        if (displayNames.Length > 0)
        {
            ShowInstallStatus(ModuleInstallStatusKind.Warning,
                localizer.GetText("ModuleRemovedMessage", string.Join(", ", displayNames)));
        }
    }

    private void NavigateToModule(ModuleSettingsItemViewModel module)
    {
        ModuleSettingsCategoryViewModel? category = categories.Values.FirstOrDefault(category => category.Contains(module));

        if (category is not null)
        {
            _ = Messenger.Send(new SettingsNavigationRequestedEventArgs(category, module.Settings));
        }
    }

    private string ResolveCategoryGlyph(string id) => id switch
    {
        GlanceModuleCategories.Information => "\uE946",
        GlanceModuleCategories.Productivity => "\uE8FD",
        GlanceModuleCategories.MediaAndCapture => "\uE8B9",
        GlanceModuleCategories.DevicesAndSystem => "\uE772",
        GlanceModuleCategories.Integrations => "\uE71B",
        _ => "\uE8B7"
    };

    private string ResolveCategoryTitle(string id) => id switch
    {
        GlanceModuleCategories.Information => localizer.GetText("InformationModulesTitle"),
        GlanceModuleCategories.Productivity => localizer.GetText("ProductivityModulesTitle"),
        GlanceModuleCategories.MediaAndCapture => localizer.GetText("MediaAndCaptureModulesTitle"),
        GlanceModuleCategories.DevicesAndSystem => localizer.GetText("DevicesAndSystemModulesTitle"),
        GlanceModuleCategories.Integrations => localizer.GetText("IntegrationsModulesTitle"),
        GlanceModuleCategories.Other => localizer.GetText("OtherModulesTitle"),
        _ => id
    };

    private string ResolveInstalledModuleNames(ModuleInstallResult result) => string.Join(", ",
        result.ComponentIds.Select(componentId => FindDisplayNameForComponent(componentId) ?? componentId));

    private void SetInstalling(bool value) => dispatcher.Dispatch(() => IsInstalling = value);

    private void ShowInstallStatus(ModuleInstallStatusKind kind,
        string message) => dispatcher.Dispatch(() =>
    {
        InstallStatusKind = kind;
        InstallStatusMessage = message;
        IsInstallStatusOpen = true;
    });
}
