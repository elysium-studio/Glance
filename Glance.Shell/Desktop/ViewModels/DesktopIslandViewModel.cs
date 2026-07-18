using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedComponent))]
    [NotifyPropertyChangedFor(nameof(PageText))]
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

    public IGlanceComponent? SelectedComponent =>
        components.Count == 0 ? null : components[SelectedIndex];

    public bool HasMultipleComponents => components.Count > 1;

    public int ComponentCount => components.Count;

    public string PageText => components.Count == 0
        ? "0 / 0"
        : $"{SelectedIndex + 1} / {components.Count}";

    [RelayCommand]
    public void MoveNext() => Move(1);

    [RelayCommand]
    public void MovePrevious() => Move(-1);

    [RelayCommand]
    public void OpenSettings() => settingsLauncher.Show();

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
        components = modulePreferences.GetActiveComponents();

        int selectedComponentIndex = selectedId is null
            ? -1
            : components
                .Select((component, index) => (component, index))
                .Where(item => string.Equals(item.component.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.index)
                .DefaultIfEmpty(-1)
                .First();

        SelectedIndex = selectedComponentIndex >= 0
            ? selectedComponentIndex
            : Math.Clamp(SelectedIndex, 0, Math.Max(0, components.Count - 1));

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
}
