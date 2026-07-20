using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using System.Collections.Generic;
using System.Linq;

namespace Glance.Shell;

public partial class DesktopIslandViewModel :
    ObservableObject,
    IDisposable
{
    [ObservableProperty]
    private bool isOpen = true;

    [ObservableProperty]
    private bool isExpanded;

    private int selectedIndex;

    private IReadOnlyList<IGlanceComponent> components;
    private readonly IGlanceAttentionService attentionService;
    private readonly ModulePreferenceService modulePreferences;
    private readonly ISettingsLauncher settingsLauncher;
    private readonly SynchronizationContext? uiContext;

    public DesktopIslandViewModel(
        ModulePreferenceService modulePreferences,
        IGlanceAttentionService attentionService,
        ISettingsLauncher settingsLauncher)
    {
        this.modulePreferences = modulePreferences;
        components = modulePreferences.GetActiveComponents();
        this.attentionService = attentionService;
        this.settingsLauncher = settingsLauncher;
        uiContext = SynchronizationContext.Current;
        attentionService.AttentionRequested += HandleAttentionRequested;
        modulePreferences.PreferencesChanged += HandlePreferencesChanged;
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

    public string PageText => components.Count == 0
        ? "0 / 0"
        : $"{SelectedIndex + 1} / {components.Count}";

    public void MoveNext() => Move(1);

    public void MovePrevious() => Move(-1);

    public void OpenSettings() => settingsLauncher.Show();

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

    public void Dispose()
    {
        attentionService.AttentionRequested -= HandleAttentionRequested;
        modulePreferences.PreferencesChanged -= HandlePreferencesChanged;
    }

    private void HandlePreferencesChanged(object? sender, EventArgs args)
    {
        if (uiContext is not null && SynchronizationContext.Current != uiContext)
        {
            uiContext.Post(_ => ApplyPreferences(), null);
            return;
        }

        ApplyPreferences();
    }

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
}
