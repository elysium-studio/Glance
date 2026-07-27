using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed partial class ModulesViewModel :
    ObservableCollectionViewModel<IModulesViewModel>,
    IReorderableSettingViewModel
{
    private readonly ModulePreferenceService preferences;
    private IReadOnlyList<ModuleSettingsItemViewModel>? originalOrder;

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
        Description = localizer.GetText("ModulesDescription/Text");
        ILookup<string, IGlanceModuleSettingViewModel> settingsByModule = settings
            .Concat(preferences.CreateRuntimeSettings())
            .OrderBy(setting => setting.Order)
            .ToLookup(setting => setting.ModuleId, StringComparer.OrdinalIgnoreCase);

        foreach (GlanceModulePreference preference in preferences.GetPreferences())
        {
            Add(CreateItem(preference, settingsByModule[preference.Id]));
        }

        preferences.ComponentsAdded += HandleComponentsAdded;
    }

    public bool CanReorder => this.OfType<ModuleSettingsItemViewModel>().Skip(1).Any();

    public string Description { get; }

    public bool SupportsReordering => CanReorder;

    [ObservableProperty]
    private bool isReordering;

    public void BeginReordering()
    {
        if (IsReordering || !CanReorder)
        {
            return;
        }

        originalOrder = [.. this.OfType<ModuleSettingsItemViewModel>()];
        SetReordering(true);
    }

    public async Task CompleteReorderingAsync()
    {
        if (!IsReordering)
        {
            return;
        }

        string[] orderedIds = [.. this.OfType<ModuleSettingsItemViewModel>().Select(item => item.Id)];
        originalOrder = null;
        SetReordering(false);
        await preferences.SetOrderAsync(orderedIds);
    }

    public void CancelReordering()
    {
        if (!IsReordering)
        {
            return;
        }

        if (originalOrder is not null)
        {
            for (int targetIndex = 0; targetIndex < originalOrder.Count; targetIndex++)
            {
                int currentIndex = IndexOf(originalOrder[targetIndex]);

                if (currentIndex >= 0 && currentIndex != targetIndex)
                {
                    Move(currentIndex, targetIndex);
                }
            }
        }

        originalOrder = null;
        SetReordering(false);
    }

    public override void Dispose()
    {
        preferences.ComponentsAdded -= HandleComponentsAdded;
        originalOrder = null;
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
            () => Messenger.Send(new ModuleReorderingRequestedEventArgs()),
            (_, enabled) => preferences.SetEnabledAsync(preference.Id, enabled));
    }

    private void HandleComponentsAdded(object? sender,
        GlanceComponentsAddedEventArgs args)
    {
        CancelReordering();

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
            Insert(Math.Min(index, Count), CreateItem(preference, settingsByModule[component.Id]));
        }
    }

    private void SetReordering(bool value)
    {
        IsReordering = value;

        foreach (ModuleSettingsItemViewModel module in this.OfType<ModuleSettingsItemViewModel>())
        {
            module.IsReordering = value;
        }
    }
}
