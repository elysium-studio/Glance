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
    private readonly Dictionary<string, ModuleSettingsCategoryViewModel> categories = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly IApplicationRestartService applicationRestart;
    private readonly IGlanceModuleCategoryResolver categoryResolver;
    private readonly IDispatcher dispatcher;
    private readonly ITextLocalizer localizer;
    private readonly ModulePreferenceService preferences;
    private readonly ModuleInstallationService installations;
    private readonly IGlanceModuleFeedService feed;
    private readonly IGlanceModulePackageService packages;

    public ModulesViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, ModulePreferenceService preferences, ModuleInstallationService installations, IGlanceModuleFeedService feed, IGlanceModulePackageService packages, IApplicationRestartService applicationRestart, ITextLocalizer localizer, IGlanceModuleCategoryResolver categoryResolver, IEnumerable<IGlanceModuleSettingViewModel> settings) :
        base(provider, factory, messenger, disposer)
    {
        this.dispatcher = dispatcher;
        this.preferences = preferences;
        this.installations = installations;
        this.feed = feed;
        this.packages = packages;
        this.applicationRestart = applicationRestart;
        this.localizer = localizer;
        this.categoryResolver = categoryResolver;
        Title = localizer.GetText("ModulesSectionTitle/Text");
        ILookup<string, IGlanceModuleSettingViewModel> settingsByModule = settings
            .Concat(preferences.CreateRuntimeSettings())
            .OrderBy(setting => setting.Order)
            .ToLookup(setting => setting.ModuleId, StringComparer.OrdinalIgnoreCase);

        foreach (GlanceModulePreference preference in preferences.GetPreferences())
        {
            IGlanceComponent? component = preferences.GetComponent(preference.Id);
            ModuleSettingsCategoryViewModel category = GetOrCreateCategory(component);
            category.Add(CreateItem(preference, settingsByModule[preference.Id]));
        }

        preferences.ComponentsAdded += HandleComponentsAdded;
        preferences.ComponentsRemoved += HandleComponentsRemoved;
        feed.FeedChanged += HandleFeedChanged;
        _ = RefreshFeedAsync();
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

    [ObservableProperty]
    private bool isFeedStatusOpen;

    [ObservableProperty]
    private string feedStatusMessage = string.Empty;

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
        feed.FeedChanged -= HandleFeedChanged;
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
                : null,
            InstallFeedModuleAsync);
    }

    private ModuleSettingsItemViewModel CreateItem(GlanceModuleFeedItem module)
    {
        ModuleSettingsItemViewModel item = new(module.Id,
            module.DisplayName,
            module.Description,
            null,
            false,
            [],
            NavigateToModule,
            (_, _) => Task.FromResult(false),
            null,
            InstallFeedModuleAsync);
        item.SetFeedItem(module, feed.IsSourceAvailable(module.FeedId), installations.GetInstalledVersion(module.Id));
        return item;
    }

    private ModuleSettingsCategoryViewModel GetOrCreateCategory(IGlanceComponent? component)
    {
        GlanceModuleCategoryDescriptor descriptor = categoryResolver.Resolve(component);
        string id = descriptor.Id;

        if (categories.TryGetValue(id, out ModuleSettingsCategoryViewModel? category))
        {
            return category;
        }

        category = new ModuleSettingsCategoryViewModel(id, descriptor.DisplayName, descriptor.Glyph, descriptor.Order, [], this);
        categories.Add(id, category);
        int index = this.TakeWhile(item => item is ModuleSettingsCategoryViewModel existing && existing.Order <= descriptor.Order).Count();
        Insert(index, category);
        return category;
    }

    private ModuleSettingsCategoryViewModel GetOrCreateCategory(GlanceModuleFeedItem module)
    {
        GlanceModuleCategoryDescriptor descriptor = categoryResolver.Resolve(module);
        string id = descriptor.Id;

        if (categories.TryGetValue(id, out ModuleSettingsCategoryViewModel? category))
        {
            return category;
        }

        category = new ModuleSettingsCategoryViewModel(id, descriptor.DisplayName, descriptor.Glyph, descriptor.Order, [], this);
        categories.Add(id, category);
        int index = this.TakeWhile(item => item is ModuleSettingsCategoryViewModel existing && existing.Order <= descriptor.Order).Count();
        Insert(index, category);
        return category;
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
            RemoveItem(component.Id);
            ModuleSettingsCategoryViewModel category = GetOrCreateCategory(component);
            string categoryId = categoryResolver.Resolve(component).Id;
            int index = orderedPreferences
                .TakeWhile(item => !string.Equals(item.Id, component.Id, StringComparison.OrdinalIgnoreCase))
                .Count(item => string.Equals(categoryResolver.Resolve(preferences.GetComponent(item.Id)).Id, categoryId, StringComparison.OrdinalIgnoreCase));
            ModuleSettingsItemViewModel item = CreateItem(preference, settingsByModule[component.Id]);
            GlanceModuleFeedItem? feedItem = feed.Modules.FirstOrDefault(module => string.Equals(module.Id, component.Id, StringComparison.OrdinalIgnoreCase));

            if (feedItem is not null)
            {
                item.SetFeedItem(feedItem, feed.IsSourceAvailable(feedItem.FeedId), installations.GetInstalledVersion(feedItem.Id));
            }

            category.Insert(Math.Min(index, category.Count), item);
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

        SynchronizeFeed();
    }

    private async Task<bool> InstallFeedModuleAsync(ModuleSettingsItemViewModel item)
    {
        if (item.FeedItem is null || !feed.IsSourceAvailable(item.FeedItem.FeedId))
        {
            return false;
        }

        ModuleInstallResult result = await packages.InstallAsync(item.FeedItem);

        if (!result.IsSuccessful)
        {
            ShowInstallStatus(ModuleInstallStatusKind.Error, string.IsNullOrWhiteSpace(result.ErrorMessage) ? localizer.GetText("ModuleInstallFailedMessage") : result.ErrorMessage);
            return false;
        }

        string installedModuleNames = ResolveInstalledModuleNames(result);
        ShowInstallStatus(ModuleInstallStatusKind.Success, result.RequiresRestart ? localizer.GetText("ModuleUpdateStagedMessage", installedModuleNames) : localizer.GetText("ModuleInstalledMessage", installedModuleNames));
        item.SetFeedItem(item.FeedItem, feed.IsSourceAvailable(item.FeedItem.FeedId), item.FeedItem.Version);
        return true;
    }

    private async Task RefreshFeedAsync()
    {
        try
        {
            await feed.RefreshAsync();
        }
        catch
        {
            dispatcher.Dispatch(() =>
            {
                IsFeedStatusOpen = true;
                FeedStatusMessage = localizer.GetText("ModuleFeedsUnavailableMessage");
            });
        }
    }

    private void HandleFeedChanged(object? sender, EventArgs args) => dispatcher.Dispatch(SynchronizeFeed);

    private void SynchronizeFeed()
    {
        bool hasUnavailableSources = feed.Sources.Any(source => !string.IsNullOrWhiteSpace(source.ErrorMessage));
        IsFeedStatusOpen = !feed.IsAvailable || hasUnavailableSources;
        FeedStatusMessage = feed.IsAvailable && hasUnavailableSources
            ? localizer.GetText("ModuleFeedsPartiallyUnavailableMessage")
            : feed.IsUsingCache
                ? localizer.GetText("ModuleFeedsCachedMessage")
                : localizer.GetText("ModuleFeedsUnavailableMessage");
        HashSet<string> feedIds = [with(StringComparer.OrdinalIgnoreCase), .. feed.Modules.Select(module => module.Id)];

        foreach (ModuleSettingsItemViewModel item in categories.Values.SelectMany(category => category.OfType<ModuleSettingsItemViewModel>()).Where(item => !item.IsInstalled && !feedIds.Contains(item.Id)).ToArray())
        {
            RemoveItem(item.Id);
        }

        foreach (GlanceModuleFeedItem module in feed.Modules.Where(module => !module.IsDelisted && module.IsVisible))
        {
            ModuleSettingsItemViewModel? item = categories.Values.SelectMany(category => category.OfType<ModuleSettingsItemViewModel>()).FirstOrDefault(item => string.Equals(item.Id, module.Id, StringComparison.OrdinalIgnoreCase));

            if (item is null)
            {
                GetOrCreateCategory(module).Add(CreateItem(module));
                continue;
            }

            item.SetFeedItem(module, feed.IsSourceAvailable(module.FeedId), installations.GetInstalledVersion(module.Id));
        }
    }

    private void RemoveItem(string id)
    {
        foreach (ModuleSettingsCategoryViewModel category in categories.Values.ToArray())
        {
            ModuleSettingsItemViewModel? item = category.OfType<ModuleSettingsItemViewModel>().FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));

            if (item is null)
            {
                continue;
            }

            _ = category.Remove(item);
            item.Dispose();

            if (category.Count == 0)
            {
                _ = Remove(category);
                _ = categories.Remove(category.Id);
            }

            return;
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
