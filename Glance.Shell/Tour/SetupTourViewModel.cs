using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace Glance.Shell;

public sealed partial class SetupTourViewModel :
    ObservableObject
{
    private const int PageCount = 5;

    private readonly IGlanceModuleCategoryResolver categoryResolver;
    private readonly IDispatcher dispatcher;
    private readonly IGlanceModuleFeedService feed;
    private readonly IGlanceModulePackageService packages;
    private readonly ModuleInstallationService installations;
    private readonly ITextLocalizer localizer;
    private readonly ILogger<SetupTourViewModel> logger;
    private readonly ModulePreferenceService preferences;
    private readonly IWritableOptions<GlanceSettings> writer;
    private bool isFinishing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(IsLastPage))]
    private int currentPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCompactModeSelected))]
    [NotifyPropertyChangedFor(nameof(IsExpandedModeSelected))]
    [NotifyPropertyChangedFor(nameof(CompactModeSelectionOpacity))]
    [NotifyPropertyChangedFor(nameof(ExpandedModeSelectionOpacity))]
    private GlanceExpansionMode expansionMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAutoHideSelected))]
    [NotifyPropertyChangedFor(nameof(IsAlwaysVisibleSelected))]
    [NotifyPropertyChangedFor(nameof(AutoHideSelectionOpacity))]
    [NotifyPropertyChangedFor(nameof(AlwaysVisibleSelectionOpacity))]
    private bool autoHide;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTopPlacementSelected))]
    [NotifyPropertyChangedFor(nameof(IsBottomPlacementSelected))]
    [NotifyPropertyChangedFor(nameof(TopPlacementSelectionOpacity))]
    [NotifyPropertyChangedFor(nameof(BottomPlacementSelectionOpacity))]
    private GlancePlacement placement;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentModule))]
    [NotifyPropertyChangedFor(nameof(ModulePosition))]
    private int selectedModuleIndex;

    public SetupTourViewModel(GlanceSettings settings, ModulePreferenceService preferences, IWritableOptions<GlanceSettings> writer, IGlanceModuleCategoryResolver categoryResolver, IGlanceModuleFeedService feed, IGlanceModulePackageService packages, ModuleInstallationService installations, IDispatcher dispatcher, ITextLocalizer localizer, ILogger<SetupTourViewModel> logger)
    {
        this.preferences = preferences;
        this.writer = writer;
        this.categoryResolver = categoryResolver;
        this.feed = feed;
        this.packages = packages;
        this.installations = installations;
        this.dispatcher = dispatcher;
        this.localizer = localizer;
        this.logger = logger;
        expansionMode = settings.ExpansionMode;
        autoHide = settings.AutoHide;
        placement = settings.Placement;
        Modules = [.. preferences.GetPreferences().Select(preference => CreateModule(preference, preferences))];
        Categories = [];
        RebuildCategories();

        preferences.ComponentsAdded += HandleComponentsAdded;
        preferences.ComponentsRemoved += HandleComponentsRemoved;
        feed.FeedChanged += HandleFeedChanged;
        _ = RefreshFeedAsync();
    }

    public event EventHandler? Finished;

    public ObservableCollection<SetupTourModuleViewModel> Modules { get; }

    public ObservableCollection<SetupTourModuleCategoryViewModel> Categories { get; }

    [ObservableProperty]
    private bool isModuleFeedStatusOpen;

    [ObservableProperty]
    private string moduleFeedStatusMessage = string.Empty;

    [ObservableProperty]
    private bool isModuleInstallStatusOpen;

    [ObservableProperty]
    private string moduleInstallStatusMessage = string.Empty;

    public int Count => PageCount;

    public bool CanGoBack => CurrentPage > 0;

    public bool CanGoNext => CurrentPage < PageCount - 1;

    public bool IsLastPage => CurrentPage == PageCount - 1;

    public bool IsCompactModeSelected => ExpansionMode != GlanceExpansionMode.AlwaysExpanded;

    public bool IsExpandedModeSelected => ExpansionMode == GlanceExpansionMode.AlwaysExpanded;

    public bool IsAutoHideSelected => AutoHide;

    public bool IsAlwaysVisibleSelected => !AutoHide;

    public bool IsTopPlacementSelected => Placement == GlancePlacement.Top;

    public bool IsBottomPlacementSelected => Placement == GlancePlacement.Bottom;

    public double CompactModeSelectionOpacity => IsCompactModeSelected ? 1 : 0;

    public double ExpandedModeSelectionOpacity => IsExpandedModeSelected ? 1 : 0;

    public double AutoHideSelectionOpacity => IsAutoHideSelected ? 1 : 0;

    public double AlwaysVisibleSelectionOpacity => IsAlwaysVisibleSelected ? 1 : 0;

    public double TopPlacementSelectionOpacity => IsTopPlacementSelected ? 1 : 0;

    public double BottomPlacementSelectionOpacity => IsBottomPlacementSelected ? 1 : 0;

    public SetupTourModuleViewModel? CurrentModule => SelectedModuleIndex >= 0 && SelectedModuleIndex < Modules.Count
        ? Modules[SelectedModuleIndex]
        : null;

    public string ModulePosition => Modules.Count == 0 ? string.Empty : $"{SelectedModuleIndex + 1} / {Modules.Count}";

    public void GoBack()
    {
        if (CanGoBack)
        {
            CurrentPage--;
        }
    }

    public void GoNext()
    {
        if (CanGoNext)
        {
            CurrentPage++;
        }
    }

    public async Task SelectExpansionModeAsync(GlanceExpansionMode mode)
    {
        ExpansionMode = mode;

        try
        {
            await writer.WriteAsync(settings => settings.ExpansionMode = mode);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to apply the setup tour expansion mode");
        }
    }

    public async Task SelectAutoHideAsync(bool value)
    {
        AutoHide = value;

        try
        {
            await writer.WriteAsync(settings => settings.AutoHide = value);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to apply the setup tour auto-hide choice");
        }
    }

    public async Task SelectPlacementAsync(GlancePlacement value)
    {
        Placement = value;

        try
        {
            await writer.WriteAsync(settings => settings.Placement = value);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to apply the setup tour placement choice");
        }
    }

    public async void Finish()
    {
        if (isFinishing)
        {
            return;
        }

        isFinishing = true;

        try
        {
            await writer.WriteAsync(settings =>
            {
                settings.ExpansionMode = ExpansionMode;
                settings.AutoHide = AutoHide;
                settings.Placement = Placement;
                settings.ShowSetupOnStartup = false;
            });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to save the setup tour choices");
        }
        finally
        {
            Finished?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Cancel()
    {
        feed.FeedChanged -= HandleFeedChanged;
        preferences.ComponentsAdded -= HandleComponentsAdded;
        preferences.ComponentsRemoved -= HandleComponentsRemoved;

    }

    private SetupTourModuleViewModel CreateModule(GlanceModulePreference preference, ModulePreferenceService preferences)
    {
        IGlanceComponent? component = preferences.GetComponent(preference.Id);
        GlanceModuleCategoryDescriptor category = categoryResolver.Resolve(component);
        return new SetupTourModuleViewModel(preference.Id,
            component?.DisplayName ?? preference.Id,
            component?.Description ?? string.Empty,
            category.Id,
            category.DisplayName,
            category.Glyph,
            category.Order,
            string.IsNullOrEmpty(component?.IconGlyph) ? category.Glyph : component.IconGlyph,
            component?.IconFontFamily ?? "Segoe Fluent Icons",
            component?.AccentResourceKey ?? "AccentTextFillColorPrimaryBrush",
            component?.CompactContent,
            component,
            true,
            dispatcher,
            InstallModuleAsync,
            RemoveModuleAsync);
    }

    private SetupTourModuleViewModel CreateModule(GlanceModuleFeedItem module)
    {
        GlanceModuleCategoryDescriptor category = categoryResolver.Resolve(module);
        SetupTourModuleViewModel viewModel = new(module.Id,
            module.DisplayName,
            module.Description,
            category.Id,
            category.DisplayName,
            category.Glyph,
            category.Order,
            module.Icon.Type == GlanceModuleIconType.Glyph ? module.Icon.Source : "\uE8B7",
            string.IsNullOrWhiteSpace(module.Icon.FontFamily) ? "Segoe Fluent Icons" : module.Icon.FontFamily,
            "AccentTextFillColorPrimaryBrush",
            null,
            null,
            false,
            dispatcher,
            InstallModuleAsync,
            RemoveModuleAsync);
        viewModel.SetFeedItem(module, feed.IsSourceAvailable(module.FeedId));
        return viewModel;
    }

    private async Task<bool> InstallModuleAsync(SetupTourModuleViewModel module)
    {
        if (module.FeedItem is null || !feed.IsSourceAvailable(module.FeedItem.FeedId))
        {
            return false;
        }

        ModuleInstallResult result = await packages.InstallAsync(module.FeedItem);

        if (!result.IsSuccessful)
        {
            logger.LogError("Failed to install module {ModuleId}: {ErrorMessage}", module.Id, result.ErrorMessage);
            dispatcher.Dispatch(() =>
            {
                ModuleInstallStatusMessage = localizer.GetText("ModuleInstallFailedMessage");
                IsModuleInstallStatusOpen = true;
            });
            return false;
        }

        _ = await preferences.SetEnabledAsync(module.Id, true);
        dispatcher.Dispatch(() => IsModuleInstallStatusOpen = false);
        return true;
    }

    private Task<bool> RemoveModuleAsync(SetupTourModuleViewModel module) => installations.UninstallAsync(module.Id);

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
                IsModuleFeedStatusOpen = true;
                ModuleFeedStatusMessage = localizer.GetText("ModuleFeedsUnavailableMessage");
            });
        }
    }

    private void HandleFeedChanged(object? sender, EventArgs args) => dispatcher.Dispatch(SynchronizeModules);

    private void HandleComponentsAdded(object? sender, GlanceComponentsAddedEventArgs args) => dispatcher.Dispatch(() =>
    {
        foreach (IGlanceComponent component in args.Components)
        {
            Modules.FirstOrDefault(module => string.Equals(module.Id, component.Id, StringComparison.OrdinalIgnoreCase))?.SetComponent(component);
        }
    });

    private void HandleComponentsRemoved(object? sender, GlanceComponentsRemovedEventArgs args) => dispatcher.Dispatch(() =>
    {
        foreach (IGlanceComponent component in args.Components)
        {
            Modules.FirstOrDefault(module => string.Equals(module.Id, component.Id, StringComparison.OrdinalIgnoreCase))?.SetComponent(null);
        }
    });

    private void SynchronizeModules()
    {
        bool hasUnavailableSources = feed.Sources.Any(source => !string.IsNullOrWhiteSpace(source.ErrorMessage));
        IsModuleFeedStatusOpen = !feed.IsAvailable || hasUnavailableSources;
        ModuleFeedStatusMessage = feed.IsAvailable && hasUnavailableSources
            ? localizer.GetText("ModuleFeedsPartiallyUnavailableMessage")
            : feed.IsUsingCache
                ? localizer.GetText("ModuleFeedsCachedMessage")
                : localizer.GetText("ModuleFeedsUnavailableMessage");

        foreach (GlanceModuleFeedItem feedItem in feed.Modules.Where(module => !module.IsDelisted && module.IsVisible))
        {
            SetupTourModuleViewModel? module = Modules.FirstOrDefault(module => string.Equals(module.Id, feedItem.Id, StringComparison.OrdinalIgnoreCase));

            if (module is null)
            {
                Modules.Add(CreateModule(feedItem));
                continue;
            }

            module.SetFeedItem(feedItem, feed.IsSourceAvailable(feedItem.FeedId));
        }

        RebuildCategories();
    }

    private void RebuildCategories()
    {
        Categories.Clear();

        foreach (IGrouping<string, SetupTourModuleViewModel> group in Modules.OrderBy(module => module.CategoryOrder).ThenBy(module => module.DisplayName).GroupBy(module => module.CategoryId, StringComparer.OrdinalIgnoreCase))
        {
            SetupTourModuleViewModel first = group.First();
            Categories.Add(new SetupTourModuleCategoryViewModel(first.CategoryId, first.CategoryDisplayName, first.CategoryGlyph, first.CategoryOrder, group));
        }

        OnPropertyChanged(nameof(ModulePosition));
    }

}
