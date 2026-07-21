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

public partial class DesktopIslandViewModel :
    ObservableViewModel,
    IRecipient<OptionsChangedEventArgs<GlanceSettings>>
{
    [ObservableProperty]
    private bool isOpen = true;

    [ObservableProperty]
    private bool isExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlacementIndex))]
    private GlancePlacement placement;

    private int selectedIndex;

    private IReadOnlyList<IGlanceComponent> components;
    private readonly IGlanceAttentionService attentionService;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<DesktopIslandViewModel> logger;
    private readonly ModulePreferenceService modulePreferences;
    private readonly INavigator navigator;

    public DesktopIslandViewModel(
        IServiceProvider provider,
        IServiceFactory factory,
        IMessenger messenger,
        IDisposer disposer,
        IDispatcher dispatcher,
        ModulePreferenceService modulePreferences,
        IGlanceAttentionService attentionService,
        INavigator navigator,
        ILogger<DesktopIslandViewModel> logger,
        GlanceSettings settings) :
        base(provider, factory, messenger, disposer)
    {
        this.dispatcher = dispatcher;
        this.modulePreferences = modulePreferences;
        components = modulePreferences.GetActiveComponents();
        this.attentionService = attentionService;
        this.navigator = navigator;
        this.logger = logger;
        Placement = settings.Placement;
        attentionService.AttentionRequested += HandleAttentionRequested;
        modulePreferences.PreferencesChanged += HandlePreferencesChanged;
        Activate();
    }

    public event EventHandler<GlanceAttentionRequest>? AttentionReceived;

    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            int normalizedIndex = Math.Clamp(
                value,
                0,
                Math.Max(0, components.Count - 1));

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

    public int PlacementIndex => (int)Placement;

    public string PageText => components.Count == 0
        ? "0 / 0"
        : $"{SelectedIndex + 1} / {components.Count}";

    public void MoveNext() => Move(1);

    public void MovePrevious() => Move(-1);

    public async void NavigateToSettings() => await NavigateAsync("SettingsWindow");

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
        modulePreferences.PreferencesChanged -= HandlePreferencesChanged;
        base.Dispose();
    }

    public void Receive(OptionsChangedEventArgs<GlanceSettings> message) =>
        dispatcher.Dispatch(() => Placement = message.Options.Placement);

    protected override void RegisterMessages() =>
        Messenger.Register<OptionsChangedEventArgs<GlanceSettings>>(this);

    private void HandlePreferencesChanged(object? sender, EventArgs args)
        => dispatcher.Dispatch(ApplyPreferences);

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
                .DefaultIfEmpty(-1)
                .First();

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
        int componentIndex = components
            .Select((component, index) => (component, index))
            .Where(item => string.Equals(
                item.component.Id,
                request.ComponentId,
                StringComparison.OrdinalIgnoreCase))
            .Select(item => item.index)
            .DefaultIfEmpty(-1)
            .First();

        if (componentIndex < 0)
        {
            return;
        }

        if (request.Level != GlanceAttentionLevel.Passive)
        {
            SelectedIndex = componentIndex;
            IsOpen = true;
            IsExpanded = request.Expand;
        }

        AttentionReceived?.Invoke(this, request);
    }

    private int FindContextComponentIndex(GlanceContentKind kind) =>
        components
            .Select((component, index) => (component, index))
            .Where(item =>
                item.component is IGlanceContextAwareComponent contextAware &&
                contextAware.CanHandle(kind))
            .Select(item => item.index)
            .DefaultIfEmpty(-1)
            .First();

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
