using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Elysium.Presentation.Abstractions;
using Glance.Application.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;

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
    [NotifyPropertyChangedFor(nameof(IsPinned))]
    private GlanceExpansionMode expansionMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlacementIndex))]
    private GlancePlacement placement;

    private int selectedIndex;

    private IReadOnlyList<IGlanceComponent> components;
    private readonly IGlanceAttentionService attentionService;
    private readonly IGlanceAssistantService assistant;
    private readonly IGlanceActionService actionService;
    private readonly IDispatcher dispatcher;
    private readonly IGlanceIntentService intentService;
    private readonly ILogger<DesktopIslandViewModel> logger;
    private readonly ModulePreferenceService modulePreferences;
    private readonly INavigator navigator;
    private readonly IWritableOptions<GlanceSettings> settingsWriter;

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
        this.assistant = assistant;
        this.actionService = actionService;
        this.intentService = intentService;
        this.navigator = navigator;
        this.logger = logger;
        this.settingsWriter = settingsWriter;
        AutoHide = settings.AutoHide;
        ExpansionMode = settings.ExpansionMode;
        Placement = settings.Placement;
        attentionService.AttentionRequested += HandleAttentionRequested;
        actionService.PresentationRequested += HandleActionPresentationRequested;
        intentService.IntentInvoked += HandleIntentInvoked;
        modulePreferences.ActiveComponentsChanged += HandleActiveComponentsChanged;
        modulePreferences.ComponentsAdded += HandleComponentsAdded;
        modulePreferences.PreferencesChanged += HandlePreferencesChanged;
        Activate();
    }

    public event EventHandler<GlanceAttentionRequest>? AttentionReceived;

    public IGlanceIntentService IntentService => intentService;

    public IGlanceAssistantService Assistant => assistant;

    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            int normalizedIndex = Math.Clamp(value, 0, Math.Max(0, components.Count - 1));

            if (!SetProperty(ref selectedIndex, normalizedIndex))
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

    public string PageText => components.Count == 0
        ? "0 / 0"
        : $"{SelectedIndex + 1} / {components.Count}";

    public void MoveNext() => Move(1);

    public void MovePrevious() => Move(-1);

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

    public bool CanHandleContent(GlanceContentKind kind) =>
        FindContextComponentIndex(kind) >= 0;

    public bool TryActivateContent(GlanceContentKind kind)
    {
        int componentIndex = FindContextComponentIndex(kind);

        if (componentIndex < 0)
        {
            return false;
        }

        SelectedIndex = componentIndex;
        IsOpen = true;
        IsExpanded = true;
        return true;
    }

    public void EndContentPreview() => IsExpanded = IsPinned;

    public async Task<bool> HandleContentAsync(GlanceContentContext context)
    {
        int componentIndex = FindContextComponentIndex(context.Kind);

        if (componentIndex < 0 ||
            components[componentIndex] is not IGlanceContextAwareComponent component)
        {
            return false;
        }

        SelectedIndex = componentIndex;
        IsOpen = true;
        IsExpanded = true;
        await component.HandleAsync(context);
        return true;
    }

    public void Move(int offset)
    {
        if (components.Count < 2)
        {
            return;
        }

        SelectedIndex = (SelectedIndex + offset + components.Count) % components.Count;
    }

    public override void Dispose()
    {
        attentionService.AttentionRequested -= HandleAttentionRequested;
        actionService.PresentationRequested -= HandleActionPresentationRequested;
        intentService.IntentInvoked -= HandleIntentInvoked;
        modulePreferences.ActiveComponentsChanged -= HandleActiveComponentsChanged;
        modulePreferences.ComponentsAdded -= HandleComponentsAdded;
        modulePreferences.PreferencesChanged -= HandlePreferencesChanged;
        base.Dispose();
    }

    public void Receive(OptionsChangedEventArgs<GlanceSettings> message) =>
        dispatcher.Dispatch(() =>
        {
            AutoHide = message.Options.AutoHide;
            ExpansionMode = message.Options.ExpansionMode;
            Placement = message.Options.Placement;
        });

    protected override void RegisterMessages() =>
        Messenger.Register<OptionsChangedEventArgs<GlanceSettings>>(this);

    private void HandlePreferencesChanged(object? sender, EventArgs args)
        => dispatcher.Dispatch(ApplyPreferences);

    private void HandleActiveComponentsChanged(object? sender, EventArgs args) =>
        dispatcher.Dispatch(ApplyPreferences);

    private void HandleComponentsAdded(object? sender, GlanceComponentsAddedEventArgs args) =>
        dispatcher.Dispatch(() =>
        {
            ApplyPreferences();

            string? componentId = args.Components
                .Select(component => component.Id)
                .FirstOrDefault(id => components.Any(component => string.Equals(component.Id, id, StringComparison.OrdinalIgnoreCase)));

            if (componentId is null)
            {
                return;
            }

            SelectedIndex = components
                .Select((component, index) => (component, index))
                .First(item => string.Equals(item.component.Id, componentId, StringComparison.OrdinalIgnoreCase))
                .index;
            IsOpen = true;
        });

    private void ApplyPreferences()
    {
        string? selectedId = SelectedComponent?.Id;
        int previousSelectedIndex = SelectedIndex;
        IReadOnlyList<IGlanceComponent> activeComponents =
            modulePreferences.GetActiveComponents();

        int selectedComponentIndex = selectedId is null
            ? -1
            : activeComponents
                .Select((component, index) => (component, index))
                .Where(item => string.Equals(item.component.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.index)
                .DefaultIfEmpty(-1).First();

        components = activeComponents;
        SelectedIndex = selectedComponentIndex >= 0
            ? selectedComponentIndex
            : Math.Clamp(previousSelectedIndex, 0, Math.Max(0, components.Count - 1));

        OnPropertyChanged(nameof(SelectedComponent));
        OnPropertyChanged(nameof(HasMultipleComponents));
        OnPropertyChanged(nameof(ComponentCount));
        OnPropertyChanged(nameof(PageText));
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
            SelectedIndex = componentIndex;
            IsOpen = true;
            IsExpanded = IsExpanded || request.Expand;
        }

        AttentionReceived?.Invoke(this, request);
    }

    private void HandleIntentInvoked(object? sender, GlanceIntentInvokedEventArgs args) =>
        dispatcher.Dispatch(() =>
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

    private void HandleActionPresentationRequested(object? sender, GlanceActionPresentationRequestedEventArgs args) =>
        dispatcher.Dispatch(() =>
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

    private int FindContextComponentIndex(GlanceContentKind kind) =>
        components
            .Select((component, index) => (component, index))
            .Where(item =>
                item.component is IGlanceContextAwareComponent contextAware &&
                contextAware.CanHandle(kind))
            .Select(item => item.index)
            .DefaultIfEmpty(-1).First();

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
