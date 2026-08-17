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

    public SetupTourViewModel(GlanceSettings settings,
        ModulePreferenceService preferences,
        IWritableOptions<GlanceSettings> writer,
        ITextLocalizer localizer,
        ILogger<SetupTourViewModel> logger)
    {
        this.preferences = preferences;
        this.writer = writer;
        this.logger = logger;
        expansionMode = settings.ExpansionMode;
        autoHide = settings.AutoHide;
        placement = settings.Placement;
        Modules = [.. preferences.GetPreferences().Select(preference => CreateModule(preference, preferences, localizer))];

        foreach (SetupTourModuleViewModel module in Modules)
        {
            module.PropertyChanged += HandleModulePropertyChanged;
        }
    }

    public event EventHandler? Finished;

    public ObservableCollection<SetupTourModuleViewModel> Modules { get; }

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
            foreach (SetupTourModuleViewModel module in Modules.Where(module => module.IsEnabled))
            {
                _ = await preferences.SetEnabledAsync(module.Id, true);
            }

            foreach (SetupTourModuleViewModel module in Modules.Where(module => !module.IsEnabled))
            {
                _ = await preferences.SetEnabledAsync(module.Id, false);
            }

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
        foreach (SetupTourModuleViewModel module in Modules)
        {
            module.PropertyChanged -= HandleModulePropertyChanged;
        }
    }

    private static SetupTourModuleViewModel CreateModule(GlanceModulePreference preference,
        ModulePreferenceService preferences,
        ITextLocalizer localizer)
    {
        IGlanceComponent? component = preferences.GetComponent(preference.Id);
        string category = component?.SettingsCategory ?? GlanceModuleCategories.Other;
        return new SetupTourModuleViewModel(preference.Id,
            component?.DisplayName ?? preference.Id,
            component?.Description ?? string.Empty,
            ResolveCategoryTitle(category, localizer),
            string.IsNullOrEmpty(component?.IconGlyph) ? ResolveGlyph(category) : component.IconGlyph,
            component?.IconFontFamily ?? "Segoe Fluent Icons",
            component?.AccentResourceKey ?? "AccentTextFillColorPrimaryBrush",
            component?.CompactContent,
            preference.IsEnabled);
    }

    private static string ResolveGlyph(string category) => category switch
    {
        GlanceModuleCategories.Information => "\uE946",
        GlanceModuleCategories.Productivity => "\uE8FD",
        GlanceModuleCategories.MediaAndCapture => "\uE8B9",
        GlanceModuleCategories.DevicesAndSystem => "\uE772",
        GlanceModuleCategories.Integrations => "\uE71B",
        _ => "\uE8B7"
    };

    private static string ResolveCategoryTitle(string category,
        ITextLocalizer localizer) => category switch
        {
            GlanceModuleCategories.Information => localizer.GetText("InformationModulesTitle"),
            GlanceModuleCategories.Productivity => localizer.GetText("ProductivityModulesTitle"),
            GlanceModuleCategories.MediaAndCapture => localizer.GetText("MediaAndCaptureModulesTitle"),
            GlanceModuleCategories.DevicesAndSystem => localizer.GetText("DevicesAndSystemModulesTitle"),
            GlanceModuleCategories.Integrations => localizer.GetText("IntegrationsModulesTitle"),
            GlanceModuleCategories.Other => localizer.GetText("OtherModulesTitle"),
            _ => category
        };

    private async void HandleModulePropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(SetupTourModuleViewModel.IsEnabled) ||
            sender is not SetupTourModuleViewModel module)
        {
            return;
        }

        if (!Modules.Any(item => item.IsEnabled))
        {
            module.IsEnabled = true;
            return;
        }

        try
        {
            _ = await preferences.SetEnabledAsync(module.Id, module.IsEnabled);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to apply the setup tour module choice for {ModuleId}", module.Id);
        }
    }
}
