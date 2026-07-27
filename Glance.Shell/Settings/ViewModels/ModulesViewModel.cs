using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed partial class ModulesViewModel :
    ObservableCollectionViewModel<IModulesViewModel>,
    ISettingViewModel
{
    private readonly ModulePreferenceService preferences;

    public ModulesViewModel(IServiceProvider provider,
        IServiceFactory factory,
        IMessenger messenger,
        IDisposer disposer,
        ModulePreferenceService preferences,
        IEnumerable<IGlanceModuleSettingViewModel> settings) :
        base(provider, factory, messenger, disposer)
    {
        this.preferences = preferences;
        ILookup<string, IGlanceModuleSettingViewModel> settingsByModule = settings
            .Concat(preferences.CreateRuntimeSettings())
            .OrderBy(setting => setting.Order)
            .ToLookup(setting => setting.ModuleId, StringComparer.OrdinalIgnoreCase);

        Add(new ModulesDescriptionViewModel());

        foreach (GlanceModulePreference preference in preferences.GetPreferences())
        {
            Add(CreateItem(preference, settingsByModule[preference.Id]));
        }

        preferences.ComponentsAdded += HandleComponentsAdded;
    }

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
            module => Messenger.Send(new ModuleSettingsNavigationRequestedEventArgs(module)),
            (_, enabled) => preferences.SetEnabledAsync(preference.Id, enabled));
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
            int index = orderedPreferences
                .Select(item => item.Id)
                .TakeWhile(id => !string.Equals(id, component.Id, StringComparison.OrdinalIgnoreCase))
                .Count();
            Insert(Math.Min(index + 1, Count), CreateItem(preference, settingsByModule[component.Id]));
        }
    }
}
