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
    private readonly Dictionary<string, SettingsCategoryViewModel> categories = new(StringComparer.OrdinalIgnoreCase);
    private readonly ITextLocalizer localizer;
    private readonly ModulePreferenceService preferences;

    public ModulesViewModel(IServiceProvider provider,
        IServiceFactory factory,
        IMessenger messenger,
        IDisposer disposer,
        ModulePreferenceService preferences,
        ITextLocalizer localizer,
        IEnumerable<IGlanceModuleSettingViewModel> settings) :
        base(provider, factory, messenger, disposer)
    {
        this.preferences = preferences;
        this.localizer = localizer;
        Title = localizer.GetText("ModulesSectionTitle/Text");
        ILookup<string, IGlanceModuleSettingViewModel> settingsByModule = settings
            .Concat(preferences.CreateRuntimeSettings())
            .OrderBy(setting => setting.Order)
            .ToLookup(setting => setting.ModuleId, StringComparer.OrdinalIgnoreCase);

        foreach (GlanceModulePreference preference in preferences.GetPreferences())
        {
            IGlanceComponent? component = preferences.GetComponent(preference.Id);
            SettingsCategoryViewModel category = GetOrCreateCategory(component?.SettingsCategory ?? GlanceModuleCategories.Other);
            category.Add(CreateItem(preference, settingsByModule[preference.Id]));
        }

        preferences.ComponentsAdded += HandleComponentsAdded;
    }

    public IReadOnlyList<ISettingViewModel> Children => [.. this];

    public string Glyph => "\uE74C";

    public string Title { get; }

    public override void Dispose()
    {
        preferences.ComponentsAdded -= HandleComponentsAdded;
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
            preference.IsEnabled,
            availableSettings.OrderBy(setting => setting.Order),
            NavigateToModule,
            (_, enabled) => preferences.SetEnabledAsync(preference.Id, enabled));
    }

    private SettingsCategoryViewModel GetOrCreateCategory(string id)
    {
        if (categories.TryGetValue(id, out SettingsCategoryViewModel? category))
        {
            return category;
        }

        category = new SettingsCategoryViewModel(id,
            ResolveCategoryTitle(id),
            ResolveCategoryGlyph(id),
            [],
            items => preferences.SetOrderAsync(items.OfType<ModuleSettingsItemViewModel>().Select(item => item.Id)));
        categories.Add(id, category);
        int categoryOrder = GetCategoryOrder(id);
        int index = this.TakeWhile(item => item is SettingsCategoryViewModel existing && GetCategoryOrder(existing.Id) <= categoryOrder).Count();
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
            SettingsCategoryViewModel category = GetOrCreateCategory(component.SettingsCategory);
            int index = orderedPreferences
                .TakeWhile(item => !string.Equals(item.Id, component.Id, StringComparison.OrdinalIgnoreCase))
                .Count(item => string.Equals(preferences.GetComponent(item.Id)?.SettingsCategory, component.SettingsCategory, StringComparison.OrdinalIgnoreCase));
            category.Insert(Math.Min(index, category.Count), CreateItem(preference, settingsByModule[component.Id]));
        }
    }

    private void NavigateToModule(ModuleSettingsItemViewModel module)
    {
        SettingsCategoryViewModel? category = categories.Values.FirstOrDefault(category => category.Contains(module));

        if (category is not null)
        {
            Messenger.Send(new SettingsNavigationRequestedEventArgs(category, module.Settings));
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
}
