using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Elysium.Presentation.Abstractions;
using Glance.Application.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace Glance.Shell;

public sealed partial class DesktopIslandViewModel :
    ObservableViewModel,
    IRecipient<OptionsChangedEventArgs<GlanceSettings>>
{
    [ObservableProperty]
    private bool autoHide;

    [ObservableProperty]
    private bool isOpen = true;

    [ObservableProperty]
    private bool isExpanded;

    [ObservableProperty]
    private bool isModuleReorderVisible;

    [ObservableProperty]
    private bool isLoadingModules = true;

    [ObservableProperty]
    private bool isTransientPresentationActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPinned))]
    private GlanceExpansionMode expansionMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayLocationIndex))]
    private GlanceDisplayLocation displayLocation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlacementIndex))]
    private GlancePlacement placement;

    private GlanceIntentDescriptor? activeContentRoute;
    private IGlanceContextAwareComponent? activeContentRouteComponent;
    private IReadOnlyList<IGlanceComponent> components;
    private GlanceContentContext? contentRoutingContext;
    private int contentRoutingPreviousIndex;
    private bool contentRoutingPreviousExpanded;
    private bool contentRoutingPreviousOpen;
    private string? attentionPresentedComponentId;
    private string? attentionPreviousComponentId;
    private bool isContentRouting;
    private bool isSelectingAttentionComponent;
    private bool isSavingModuleOrder;
    private readonly IGlanceAttentionService attentionService;
    private readonly IGlanceActionService actionService;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<DesktopIslandViewModel> logger;
    private readonly ModulePreferenceService modulePreferences;
    private readonly INavigator navigator;
    private readonly IWritableOptions<GlanceSettings> settingsWriter;
    private readonly HashSet<IGlanceTransientComponent> transientComponents = [];
    private bool transientExpansionLocked;
    private bool transientPreviousExpanded;
    private bool transientPreviousOpen;

    public DesktopIslandViewModel(IServiceProvider provider,
        IServiceFactory factory,
        IMessenger messenger,
        IDisposer disposer,
        IDispatcher dispatcher,
        ModulePreferenceService modulePreferences,
        IGlanceAttentionService attentionService,
        IGlanceAssistantService assistant,
        IGlanceActionService actionService,
        IGlanceIntentService intentService,
        INavigator navigator,
        ILogger<DesktopIslandViewModel> logger,
        GlanceSettings settings,
        IWritableOptions<GlanceSettings> settingsWriter) :
        base(provider, factory, messenger, disposer)
    {
        this.dispatcher = dispatcher;
        this.modulePreferences = modulePreferences;
        components = modulePreferences.GetActiveComponents();
        this.attentionService = attentionService;
        Assistant = assistant;
        this.actionService = actionService;
        IntentService = intentService;
        this.navigator = navigator;
        this.logger = logger;
        this.settingsWriter = settingsWriter;
        AutoHide = settings.AutoHide;
        ExpansionMode = settings.ExpansionMode;
        DisplayLocation = settings.DisplayLocation;
        Placement = settings.Placement;
        attentionService.AttentionRequested += HandleAttentionRequested;
        actionService.PresentationRequested += HandleActionPresentationRequested;
        intentService.IntentInvoked += HandleIntentInvoked;
        modulePreferences.ActiveComponentsChanged += HandleActiveComponentsChanged;
        modulePreferences.ComponentsAdded += HandleComponentsAdded;
        modulePreferences.ComponentsRemoved += HandleComponentsRemoved;
        modulePreferences.PreferencesChanged += HandlePreferencesChanged;
        TrackTransientComponents(modulePreferences.GetTransientComponents());
        Activate();
    }

    public event EventHandler<GlanceAttentionRequest>? AttentionReceived;

    public IGlanceIntentService IntentService { get; }

    public IGlanceAssistantService Assistant { get; }

    public ObservableCollection<IGlanceComponent> ModuleOrder { get; } = [];

    public IReadOnlyList<GlanceContentRoute> ContentRoutes { get; private set; } = [];

    public IGlanceTransientComponent? TransientComponent { get; private set; }

    public bool IsTransientExpansionLocked => TransientComponent is not null && transientExpansionLocked;

    public bool IsContentRoutePickerVisible { get; private set; }

    public int SelectedIndex
    {
        get; set
        {
            int normalizedIndex = Math.Clamp(value, 0, Math.Max(0, components.Count - 1));

            if (!isSelectingAttentionComponent &&
                attentionPresentedComponentId is not null &&
                normalizedIndex >= 0 &&
                normalizedIndex < components.Count &&
                !string.Equals(components[normalizedIndex].Id,
                    attentionPresentedComponentId,
                    StringComparison.OrdinalIgnoreCase))
            {
                ClearAttentionRestoration();
            }

            if (!SetProperty(ref field, normalizedIndex))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedComponent));
            OnPropertyChanged(nameof(PageText));
        }
    }

    public IGlanceComponent? SelectedComponent =>
        SelectedIndex >= 0 && SelectedIndex < components.Count
            ? components[SelectedIndex]
            : null;

    public bool HasMultipleComponents => components.Count > 1;

    public int ComponentCount => components.Count;

    public bool IsPinned => ExpansionMode == GlanceExpansionMode.AlwaysExpanded;

    public int PlacementIndex => (int)Placement;

    public int DisplayLocationIndex => (int)DisplayLocation;

    public string PageText => components.Count == 0
        ? "0 / 0"
        : $"{SelectedIndex + 1} / {components.Count}";

    public void MoveNext() => Move(1);

    public void MovePrevious() => Move(-1);

    public void CompleteModuleLoading() => IsLoadingModules = false;

    public void BeginModuleReorder()
    {
        if (components.Count < 2 || IsModuleReorderVisible)
        {
            return;
        }

        ModuleOrder.Clear();

        foreach (IGlanceComponent component in components)
        {
            ModuleOrder.Add(component);
        }

        IsOpen = true;
        IsExpanded = true;
        IsModuleReorderVisible = true;
    }

    public void CancelModuleReorder() => IsModuleReorderVisible = false;

    public async void ConfirmModuleReorder()
    {
        if (!IsModuleReorderVisible || isSavingModuleOrder)
        {
            return;
        }

        isSavingModuleOrder = true;

        try
        {
            await modulePreferences.SetOrderAsync(ModuleOrder.Select(component => component.Id));
            IsModuleReorderVisible = false;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to save the Glance module order");
        }
        finally
        {
            isSavingModuleOrder = false;
        }
    }

    public void ShowComponent(string componentId)
    {
        int componentIndex = components
            .Select((component, index) => (component, index))
            .Where(item => string.Equals(item.component.Id, componentId, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.index)
            .DefaultIfEmpty(-1).First();

        if (componentIndex < 0)
        {
            return;
        }

        SelectedIndex = componentIndex;
        IsOpen = true;
        IsExpanded = true;
    }

    public void CompleteStartup() => attentionService.CompleteStartup();

    public async void NavigateToSettings() => await NavigateAsync("SettingsWindow");

    public async void TogglePinned()
    {
        GlanceExpansionMode previousMode = ExpansionMode;
        GlanceExpansionMode nextMode = IsPinned
            ? GlanceExpansionMode.ExpandOnHover
            : GlanceExpansionMode.AlwaysExpanded;
        ExpansionMode = nextMode;

        try
        {
            await settingsWriter.WriteAsync(settings => settings.ExpansionMode = nextMode);
        }
        catch (Exception exception)
        {
            ExpansionMode = previousMode;
            logger.LogError(exception, "Failed to change the Glance expansion mode");
        }
    }

    public async void ToggleDisplayLocation()
    {
        GlanceDisplayLocation previousLocation = DisplayLocation;
        GlanceDisplayLocation nextLocation = DisplayLocation == GlanceDisplayLocation.Taskbar
            ? GlanceDisplayLocation.DesktopEdge
            : GlanceDisplayLocation.Taskbar;
        DisplayLocation = nextLocation;

        try
        {
            await settingsWriter.WriteAsync(settings => settings.DisplayLocation = nextLocation);
        }
        catch (Exception exception)
        {
            DisplayLocation = previousLocation;
            logger.LogError(exception, "Failed to change the Glance display location");
        }
    }

    public bool CanHandleContent(GlanceContentKind kind) => IntentService.GetIntents(kind).Count > 0;

    public bool TryActivateContent(GlanceContentContext context)
    {
        IReadOnlyList<GlanceContentRoute> routes = GetContentRoutes(context);

        if (routes.Count == 0)
        {
            return false;
        }

        if (!isContentRouting)
        {
            isContentRouting = true;
            contentRoutingContext = context;
            contentRoutingPreviousIndex = SelectedIndex;
            contentRoutingPreviousExpanded = IsExpanded;
            contentRoutingPreviousOpen = IsOpen;
        }
        else if (contentRoutingContext == context)
        {
            return true;
        }

        SetContentRoutes(routes);
        GlanceContentRoute? currentRoute = routes.FirstOrDefault(route =>
            string.Equals(route.TargetComponentId, SelectedComponent?.Id, StringComparison.OrdinalIgnoreCase));

        if (currentRoute is not null)
        {
            ActivateContentRoute(currentRoute.Intent);
            return true;
        }

        IsOpen = true;
        IsExpanded = true;

        if (routes.Count == 1)
        {
            ActivateContentRoute(routes[0].Intent);
            return true;
        }

        activeContentRoute = null;
        SetContentRoutePickerVisible(true);
        return true;
    }

    public bool TryActivateContentRoute(string intentId)
    {
        GlanceContentRoute? route = ContentRoutes.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, intentId, StringComparison.OrdinalIgnoreCase));

        if (!isContentRouting || route is null)
        {
            return false;
        }

        ActivateContentRoute(route.Intent);
        return true;
    }

    public void EndContentPreview() => CompleteContentRouting(false);

    public void CompleteContentRouting(bool contentHandled)
    {
        if (!isContentRouting)
        {
            IsExpanded = contentHandled || IsPinned;
            return;
        }

        isContentRouting = false;
        IGlanceContextAwareComponent? routeComponent = activeContentRouteComponent;
        activeContentRoute = null;
        activeContentRouteComponent = null;
        contentRoutingContext = null;
        SetContentRoutePickerVisible(false);
        SetContentRoutes([]);

        if (contentHandled)
        {
            IsOpen = true;
            IsExpanded = true;
            return;
        }

        routeComponent?.EndContentPreview();

        SelectedIndex = contentRoutingPreviousIndex;
        IsOpen = contentRoutingPreviousOpen;
        IsExpanded = contentRoutingPreviousExpanded || IsPinned;
    }

    public async Task<bool> HandleContentAsync(GlanceContentContext context)
    {
        if (isContentRouting && activeContentRoute is null)
        {
            return false;
        }

        int componentIndex = activeContentRoute is null
            ? FindContextComponentIndex(context)
            : FindComponentIndex(activeContentRoute.TargetComponentId);

        if (componentIndex < 0 ||
            components[componentIndex] is not IGlanceContextAwareComponent component)
        {
            return false;
        }

        SelectedIndex = componentIndex;
        IsOpen = true;
        IsExpanded = true;

        if (component is IGlanceContentHandlingResultComponent resultComponent)
        {
            return await resultComponent.TryHandleAsync(context);
        }

        await component.HandleAsync(context);
        return true;
    }

    public void Move(int offset)
    {
        if (TransientComponent is not null || components.Count < 2)
        {
            return;
        }

        SelectedIndex = (SelectedIndex + offset + components.Count) % components.Count;
    }

    public override void Dispose()
    {
        attentionService.AttentionRequested -= HandleAttentionRequested;
        actionService.PresentationRequested -= HandleActionPresentationRequested;
        IntentService.IntentInvoked -= HandleIntentInvoked;
        modulePreferences.ActiveComponentsChanged -= HandleActiveComponentsChanged;
        modulePreferences.ComponentsAdded -= HandleComponentsAdded;
        modulePreferences.ComponentsRemoved -= HandleComponentsRemoved;
        modulePreferences.PreferencesChanged -= HandlePreferencesChanged;
        UntrackTransientComponents([.. transientComponents]);
        base.Dispose();
    }

    public void Receive(OptionsChangedEventArgs<GlanceSettings> message) => dispatcher.Dispatch(() =>
                                                                                 {
                                                                                     bool restoreExpansionState = AutoHide && !message.Options.AutoHide;
                                                                                     ExpansionMode = message.Options.ExpansionMode;
                                                                                     AutoHide = message.Options.AutoHide;
                                                                                     DisplayLocation = message.Options.DisplayLocation;
                                                                                     Placement = message.Options.Placement;

                                                                                     if (restoreExpansionState)
                                                                                     {
                                                                                         IsExpanded = IsPinned;
                                                                                     }
                                                                                 });

    protected override void RegisterMessages() => Messenger.Register<OptionsChangedEventArgs<GlanceSettings>>(this);

    private void HandlePreferencesChanged(object? sender, EventArgs args) => dispatcher.Dispatch(ApplyPreferences);

    private void HandleActiveComponentsChanged(object? sender, EventArgs args) => dispatcher.Dispatch(ApplyPreferences);

    private void HandleComponentsAdded(object? sender,
        GlanceComponentsAddedEventArgs args) => dispatcher.Dispatch(() =>
        {
            TrackTransientComponents(args.Components.OfType<IGlanceTransientComponent>());
            ApplyPreferences();
        });

    private void HandleComponentsRemoved(object? sender,
        GlanceComponentsRemovedEventArgs args) => dispatcher.Dispatch(() =>
        {
            IGlanceTransientComponent[] removed = [.. args.Components.OfType<IGlanceTransientComponent>()];
            UntrackTransientComponents(removed);

            if (TransientComponent is not null && removed.Contains(TransientComponent))
            {
                DismissTransientPresentation();
                CompleteTransientPresentationDismissal();
            }

            ApplyPreferences();
        });

    private void ApplyPreferences()
    {
        if (TransientComponent is not null && !modulePreferences.IsEnabled(TransientComponent.Id))
        {
            DismissTransientPresentation();
        }

        string? selectedId = SelectedComponent?.Id;
        int previousSelectedIndex = SelectedIndex;
        IReadOnlyList<IGlanceComponent> activeComponents =
            modulePreferences.GetActiveComponents();

        string? restorationId = string.Equals(selectedId,
            attentionPresentedComponentId,
            StringComparison.OrdinalIgnoreCase) &&
            !activeComponents.Any(component => string.Equals(component.Id,
                attentionPresentedComponentId,
                StringComparison.OrdinalIgnoreCase))
            ? attentionPreviousComponentId
            : null;

        int selectedComponentIndex = selectedId is null
            ? -1
            : activeComponents
                .Select((component, index) => (component, index))
                .Where(item => string.Equals(item.component.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.index)
                .DefaultIfEmpty(-1).First();

        int restoredComponentIndex = restorationId is null
            ? -1
            : activeComponents
                .Select((component, index) => (component, index))
                .Where(item => string.Equals(item.component.Id,
                    restorationId,
                    StringComparison.OrdinalIgnoreCase))
                .Select(item => item.index)
                .DefaultIfEmpty(-1).First();

        components = activeComponents;
        isSelectingAttentionComponent = true;

        try
        {
            SelectedIndex = selectedComponentIndex >= 0
                ? selectedComponentIndex
                : restoredComponentIndex >= 0
                    ? restoredComponentIndex
                    : Math.Clamp(previousSelectedIndex, 0, Math.Max(0, components.Count - 1));
        }
        finally
        {
            isSelectingAttentionComponent = false;
        }

        if (restorationId is not null ||
            attentionPresentedComponentId is not null &&
            !components.Any(component => string.Equals(component.Id,
                attentionPresentedComponentId,
                StringComparison.OrdinalIgnoreCase)))
        {
            ClearAttentionRestoration();
        }

        OnPropertyChanged(nameof(SelectedComponent));
        OnPropertyChanged(nameof(HasMultipleComponents));
        OnPropertyChanged(nameof(ComponentCount));
        OnPropertyChanged(nameof(PageText));
    }

    private void TrackTransientComponents(IEnumerable<IGlanceTransientComponent> components)
    {
        foreach (IGlanceTransientComponent component in components)
        {
            if (!transientComponents.Add(component))
            {
                continue;
            }

            component.PresentationRequested += HandleTransientPresentationRequested;
            component.DismissalRequested += HandleTransientDismissalRequested;
        }
    }

    private void UntrackTransientComponents(IEnumerable<IGlanceTransientComponent> components)
    {
        foreach (IGlanceTransientComponent component in components)
        {
            component.PresentationRequested -= HandleTransientPresentationRequested;
            component.DismissalRequested -= HandleTransientDismissalRequested;
            _ = transientComponents.Remove(component);
        }
    }

    private void HandleTransientPresentationRequested(object? sender,
        GlanceTransientPresentationRequestedEventArgs args) => dispatcher.Dispatch(() =>
        {
            if (sender is not IGlanceTransientComponent component ||
                !transientComponents.Contains(component) ||
                !modulePreferences.IsEnabled(component.Id))
            {
                return;
            }

            bool isActivating = TransientComponent is null;

            if (isActivating)
            {
                transientPreviousOpen = IsOpen;
                transientPreviousExpanded = IsExpanded;
                transientExpansionLocked = IsExpanded || args.Expand;
            }
            else if (args.Expand && !transientExpansionLocked)
            {
                transientExpansionLocked = true;
                OnPropertyChanged(nameof(IsTransientExpansionLocked));
            }

            if (!ReferenceEquals(TransientComponent, component))
            {
                TransientComponent = component;
                OnPropertyChanged(nameof(TransientComponent));
            }

            if (!IsTransientPresentationActive)
            {
                IsTransientPresentationActive = true;
                OnPropertyChanged(nameof(IsTransientExpansionLocked));
            }

            IsOpen = true;

            if (args.Expand)
            {
                IsExpanded = true;
            }
        });

    private void HandleTransientDismissalRequested(object? sender,
        EventArgs args) => dispatcher.Dispatch(() =>
        {
            if (ReferenceEquals(sender, TransientComponent))
            {
                DismissTransientPresentation();
            }
        });

    private void DismissTransientPresentation()
    {
        if (TransientComponent is null || !IsTransientPresentationActive)
        {
            return;
        }

        IsTransientPresentationActive = false;
    }

    public void CompleteTransientPresentationDismissal()
    {
        if (TransientComponent is null || IsTransientPresentationActive)
        {
            return;
        }

        TransientComponent = null;
        OnPropertyChanged(nameof(TransientComponent));
        transientExpansionLocked = false;
        OnPropertyChanged(nameof(IsTransientExpansionLocked));
        IsOpen = transientPreviousOpen;
        IsExpanded = IsPinned || transientPreviousExpanded;
    }

    private void HandleAttentionRequested(object? sender, GlanceAttentionRequest request)
    {
        if (!modulePreferences.IsAttentionEnabled(request.ComponentId))
        {
            return;
        }

        int componentIndex = components
            .Select((component, index) => (component, index))
            .Where(item => string.Equals(item.component.Id, request.ComponentId, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.index)
            .DefaultIfEmpty(-1).First();

        if (componentIndex < 0)
        {
            return;
        }

        bool isAlreadyPresented = SelectedIndex == componentIndex && IsOpen && (!request.Expand || IsExpanded);

        if (isAlreadyPresented)
        {
            return;
        }

        if (request.Level != GlanceAttentionLevel.Passive)
        {
            IGlanceComponent attentionComponent = components[componentIndex];

            if (attentionComponent is IGlanceAvailabilityComponent &&
                !string.Equals(SelectedComponent?.Id,
                    request.ComponentId,
                    StringComparison.OrdinalIgnoreCase))
            {
                attentionPreviousComponentId = SelectedComponent?.Id;
                attentionPresentedComponentId = request.ComponentId;
            }

            isSelectingAttentionComponent = true;

            try
            {
                SelectedIndex = componentIndex;
            }
            finally
            {
                isSelectingAttentionComponent = false;
            }

            IsOpen = true;
            IsExpanded = IsExpanded || request.Expand;
        }

        AttentionReceived?.Invoke(this, request);
    }

    private void HandleIntentInvoked(object? sender, GlanceIntentInvokedEventArgs args) => dispatcher.Dispatch(() =>
                                                                                                {
                                                                                                    int componentIndex = components
                                                                                                        .Select((component, index) => (component, index))
                                                                                                        .Where(item => string.Equals(item.component.Id, args.TargetComponentId, StringComparison.OrdinalIgnoreCase))
                                                                                                        .Select(item => item.index)
                                                                                                        .DefaultIfEmpty(-1).First();

                                                                                                    if (componentIndex < 0)
                                                                                                    {
                                                                                                        return;
                                                                                                    }

                                                                                                    SelectedIndex = componentIndex;
                                                                                                    IsOpen = true;
                                                                                                    IsExpanded = true;
                                                                                                    AttentionReceived?.Invoke(this, new GlanceAttentionRequest(args.TargetComponentId));
                                                                                                });

    private void HandleActionPresentationRequested(object? sender, GlanceActionPresentationRequestedEventArgs args) => dispatcher.Dispatch(() =>
                                                                                                                            {
                                                                                                                                int componentIndex = components
                                                                                                                                    .Select((component, index) => (component, index))
                                                                                                                                    .Where(item => string.Equals(item.component.Id, args.TargetComponentId, StringComparison.OrdinalIgnoreCase))
                                                                                                                                    .Select(item => item.index)
                                                                                                                                    .DefaultIfEmpty(-1).First();

                                                                                                                                if (componentIndex < 0)
                                                                                                                                {
                                                                                                                                    return;
                                                                                                                                }

                                                                                                                                SelectedIndex = componentIndex;
                                                                                                                                IsOpen = true;
                                                                                                                                IsExpanded = IsExpanded || args.Presentation == GlanceActionPresentation.Expanded || IsPinned;
                                                                                                                                AttentionReceived?.Invoke(this, new GlanceAttentionRequest(args.TargetComponentId));
                                                                                                                            });

    private int FindContextComponentIndex(GlanceContentContext context) => components
            .Select((component, index) => (component, index))
            .Where(item =>
                item.component is IGlanceContextAwareComponent contextAware &&
                contextAware.CanHandle(context))
            .Select(item => item.index)
            .DefaultIfEmpty(-1).First();

    private void ClearAttentionRestoration()
    {
        attentionPresentedComponentId = null;
        attentionPreviousComponentId = null;
    }

    private int FindComponentIndex(string componentId) => components
            .Select((component, index) => (component, index))
            .Where(item => string.Equals(item.component.Id, componentId, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.index)
            .DefaultIfEmpty(-1).First();

    private IReadOnlyList<GlanceContentRoute> GetContentRoutes(GlanceContentContext context)
    {
        HashSet<string> compatibleComponents =
        [
            with(StringComparer.OrdinalIgnoreCase),
            .. IntentService.GetIntents(context)
                .Select(intent => modulePreferences.GetComponent(intent.TargetComponentId))
                .OfType<IGlanceContextAwareComponent>()
                .Where(component => component.CanHandle(context))
                .OfType<IGlanceComponent>()
                .Select(component => component.Id)
        ];
        return
        [
            .. IntentService.GetIntents(context)
                .Where(intent => compatibleComponents.Contains(intent.TargetComponentId))
                .GroupBy(intent => intent.TargetComponentId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Select(intent => new GlanceContentRoute(intent,
                    modulePreferences.GetComponent(intent.TargetComponentId)!))
                .OrderBy(route => route.TargetComponent.Order)
        ];
    }

    private void ActivateContentRoute(GlanceIntentDescriptor route)
    {
        if (contentRoutingContext is null ||
            modulePreferences.GetComponent(route.TargetComponentId) is not IGlanceContextAwareComponent component)
        {
            return;
        }

        if (!ReferenceEquals(activeContentRouteComponent, component))
        {
            activeContentRouteComponent?.EndContentPreview();
            component.BeginContentPreview(contentRoutingContext);
            activeContentRouteComponent = component;
            ApplyPreferences();
        }

        int componentIndex = FindComponentIndex(route.TargetComponentId);

        if (componentIndex < 0)
        {
            return;
        }

        activeContentRoute = route;
        SelectedIndex = componentIndex;
        IsOpen = true;
        IsExpanded = true;
        SetContentRoutePickerVisible(false);
    }

    private void SetContentRoutes(IReadOnlyList<GlanceContentRoute> routes)
    {
        ContentRoutes = routes;
        OnPropertyChanged(nameof(ContentRoutes));
    }

    private void SetContentRoutePickerVisible(bool value)
    {
        if (IsContentRoutePickerVisible == value)
        {
            return;
        }

        IsContentRoutePickerVisible = value;
        OnPropertyChanged(nameof(IsContentRoutePickerVisible));
    }

    private async Task NavigateAsync(string key)
    {
        try
        {
            await navigator.NavigateAsync(key);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to navigate to {NavigationKey}", key);
        }
    }
}
