using Elysium.Platform.Abstractions;
using Elysium.UI.Controls.WinUI;
using Glance.Application.Abstractions;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Threading.Tasks;

namespace Glance.Shell.WinUI;

public sealed partial class DesktopIslandView :
    DesktopIsland,
    IDesktopIslandAnimationHost,
    IDesktopIslandComponentHost,
    IDesktopIslandDropHost,
    IDesktopIslandModuleReorderHost,
    IDesktopIslandPresentationHost
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly IDesktopIslandAnimationController animationController;
    private readonly IDesktopIslandComponentController componentController;
    private readonly IDesktopIslandDropController dropController;
    private readonly IDesktopIslandModuleReorderController moduleReorderController;
    private readonly IDesktopIslandPresentationController presentationController;
    private readonly IDesktopIslandDisplayController displayController;
    private readonly IDesktopIslandScreenTargetProvider screenTargetProvider;

    public DesktopIslandView(IDesktopIslandAnimationController animationController, IDesktopIslandComponentController componentController, IDesktopIslandDisplayController displayController, IDesktopIslandDropController dropController, IDesktopIslandModuleReorderController moduleReorderController, IDesktopIslandPresentationController presentationController, IDesktopIslandScreenTargetProvider screenTargetProvider, IDesktopIslandBindings bindings)
    {
        InitializeComponent();

        this.animationController = animationController;
        this.componentController = componentController;
        this.displayController = displayController;
        this.dropController = dropController;
        this.moduleReorderController = moduleReorderController;
        this.presentationController = presentationController;
        this.screenTargetProvider = screenTargetProvider;

        BindingPolicy = bindings;
        dispatcherQueue = DispatcherQueue;

        Opened += HandleIslandOpened;
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;

        AddHandler(PointerPressedEvent, new PointerEventHandler(HandleButtonPointerPressed), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler(HandleButtonPointerReleased), true);
        AddHandler(PointerCanceledEvent, new PointerEventHandler(HandleButtonPointerCanceled), true);
        AddHandler(PointerCaptureLostEvent, new PointerEventHandler(HandleButtonPointerCaptureLost), true);
        ContentRouteScrollViewer.AddHandler(DragOverEvent, new DragEventHandler(HandleContentRoutePickerDragOver), true);
        ContentRouteScrollViewer.AddHandler(DragLeaveEvent, new DragEventHandler(HandleContentRoutePickerDragLeave), true);
    }

    public IDesktopIslandBindings BindingPolicy { get; }

    public DesktopIslandViewModel ViewModel => (DesktopIslandViewModel)DataContext;

    private object? GetModuleBackgroundContent() => BindingPolicy.ToBackgroundContent(ViewModel.SelectedComponent, ViewModel.IsLoadingModules, ViewModel.IsTransientPresentationActive);

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        animationController.Attach(this);
        componentController.Attach(this);
        dropController.Attach(this);
        moduleReorderController.Attach(this);
        presentationController.Attach(this);

        RefreshDisplayLocationIcons();
        ViewModel.PropertyChanged += HandleViewModelPropertyChanged;
        ActualThemeChanged += HandleActualThemeChanged;
        (ViewModel.IntentService as GlanceIntentService)?.SetPresentationTargetProvider(GetIntentPresentationTarget);
        Deactivated += HandleIslandDeactivated;

        _ = DispatcherQueue.TryEnqueue(InitializeExpansionState);
        presentationController.Initialize();
        componentController.VisibilityChanged();
        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, ApplyIslandActivationMode);
    }

    private void HandleIslandOpened(object sender, RoutedEventArgs args) => _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, ApplyIslandActivationMode);

    private void ApplyIslandActivationMode() => componentController.ApplyActivationMode();

    private void InitializeExpansionState()
    {
        ViewModel.IsExpanded = ViewModel.IsPinned;
        componentController.ApplyExpansionLock();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        animationController.Detach();
        componentController.Detach();
        dropController.Detach();
        moduleReorderController.Detach();
        presentationController.Detach();

        ViewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        ActualThemeChanged -= HandleActualThemeChanged;
        (ViewModel.IntentService as GlanceIntentService)?.SetPresentationTargetProvider(null);
        Deactivated -= HandleIslandDeactivated;
    }

    private void HandleModuleReorderListLoaded(object sender, RoutedEventArgs args) => moduleReorderController.ListLoaded();

    private void HandleModuleReorderEdgeFadeHostSizeChanged(object sender, SizeChangedEventArgs args) => moduleReorderController.EdgeFadeHostSizeChanged();

    private void HandleModuleReorderPointerWheelChanged(object sender, PointerRoutedEventArgs args) => moduleReorderController.PointerWheelChanged(args);

    private void HandlePreviousModuleOrderClicked(object sender, RoutedEventArgs args) => moduleReorderController.Previous();

    private void HandleNextModuleOrderClicked(object sender, RoutedEventArgs args) => moduleReorderController.Next();

    private GlanceScreenRectangle? GetIntentPresentationTarget() => screenTargetProvider.GetTarget(new WindowHandle(Handle));

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!dispatcherQueue.HasThreadAccess)
        {
            _ = dispatcherQueue.TryEnqueue(() => HandleViewModelPropertyChanged(sender, args));
            return;
        }

        if (args.PropertyName == nameof(DesktopIslandViewModel.DisplayLocation))
        {
            RefreshDisplayLocationIcons();
        }

        if (args.PropertyName == nameof(DesktopIslandViewModel.IsModuleReorderVisible))
        {
            presentationController.ModuleReorderVisibilityChanged();
            return;
        }

        if (args.PropertyName == nameof(DesktopIslandViewModel.IsContentRoutePickerVisible))
        {
            if (ViewModel.IsContentRoutePickerVisible)
            {
                animationController.CancelConnectedAnimation();
                dropController.ResetRoutePicker();
            }

            presentationController.ContentRouteVisibilityChanged();
            return;
        }

        if (args.PropertyName == nameof(DesktopIslandViewModel.SelectedComponent))
        {
            componentController.SelectedComponentChanged();
            presentationController.SelectedComponentChanged();
            return;
        }

        if (args.PropertyName == nameof(DesktopIslandViewModel.IsTransientPresentationActive))
        {
            presentationController.TransientPresentationChanged();
            return;
        }

        if (args.PropertyName == nameof(DesktopIslandViewModel.IsTransientExpansionLocked))
        {
            ApplyExpansionLock();
            return;
        }

        if (args.PropertyName == nameof(DesktopIslandViewModel.IsLoadingModules))
        {
            presentationController.LoadingModulesChanged();
            return;
        }

        if (args.PropertyName == nameof(DesktopIslandViewModel.IsOpen))
        {
            componentController.VisibilityChanged();
            return;
        }

        if (args.PropertyName == nameof(DesktopIslandViewModel.IsPinned))
        {
            ApplyExpansionLock();
            return;
        }

        if (args.PropertyName == nameof(DesktopIslandViewModel.IsExpanded))
        {
            animationController.ExpandedChanged();
            return;
        }

        if (args.PropertyName != nameof(DesktopIslandViewModel.SelectedIndex))
        {
            return;
        }

        animationController.SelectedIndexChanged();
    }

    private void HandleActualThemeChanged(FrameworkElement sender, object args) => componentController.ThemeChanged();

    private void HandleModuleOrderItemPointerEntered(object sender, PointerRoutedEventArgs args) => moduleReorderController.ItemPointerEntered(sender);

    private void HandleModuleOrderItemPointerExited(object sender, PointerRoutedEventArgs args) => moduleReorderController.ItemPointerExited(sender);

    private void HandleModuleReorderDragStarting(object sender, DragItemsStartingEventArgs args) => moduleReorderController.DragStarting(args);

    private void HandleModuleReorderDragCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) => moduleReorderController.DragCompleted();

    private void HandleModuleReorderDragOver(object sender, DragEventArgs args) => moduleReorderController.DragOver(args);

    private async void HandleModuleReorderDragStartingVisual(UIElement sender, DragStartingEventArgs args) => await moduleReorderController.CreateDragVisualAsync(args);

    private void HandleIslandPointerEntered(object sender, PointerRoutedEventArgs args)
    {
        RefreshDisplayLocationIcons();
        presentationController.StopAttentionExpansion();
        componentController.PointerEntered();

        if (ViewModel.ExpansionMode == GlanceExpansionMode.ExpandOnClick && !ViewModel.IsExpanded)
        {
            return;
        }

        Reveal();
        ViewModel.IsExpanded = true;
    }

    private void RefreshDisplayLocationIcons()
    {
        DesktopIslandDisplayIcons icons = displayController.GetIcons(new WindowHandle(Handle));
        MoveToTaskbarIcon.Glyph = icons.MoveToTaskbarGlyph;
        MoveToTaskbarIconTransform.Angle = icons.MoveToTaskbarAngle;
        MoveToScreenEdgeIcon.Glyph = icons.MoveToScreenEdgeGlyph;
        MoveToScreenEdgeIconTransform.Angle = icons.MoveToScreenEdgeAngle;
    }

    private void HandleIslandPointerExited(object sender, PointerRoutedEventArgs args) => componentController.PointerExited();

    private void HandleButtonPointerPressed(object sender, PointerRoutedEventArgs args) => componentController.ButtonPressed(args);

    private void HandleButtonPointerReleased(object sender, PointerRoutedEventArgs args) => componentController.ButtonReleased();

    private void HandleButtonPointerCanceled(object sender, PointerRoutedEventArgs args) => componentController.ButtonReleased();

    private void HandleButtonPointerCaptureLost(object sender, PointerRoutedEventArgs args) => componentController.ButtonReleased();

    private void ApplyExpansionLock() => componentController.ApplyExpansionLock();

    private void HandleIslandDeactivated(object? sender, EventArgs args) => componentController.IslandDeactivated();

    private void HandlePointerWheelChanged(object sender, PointerRoutedEventArgs args)
    {
        if (ViewModel.TransientComponent is not null)
        {
            args.Handled = true;
            return;
        }

        if (ViewModel.IsModuleReorderVisible)
        {
            return;
        }

        int delta = args.GetCurrentPoint(this).Properties.MouseWheelDelta;

        if (delta != 0)
        {
            ViewModel.Move(delta < 0 ? 1 : -1);
            args.Handled = true;
        }
    }

    private async void HandleDragEnter(object sender, DragEventArgs args) => await dropController.EnterAsync(args);

    private void HandleDragOver(object sender, DragEventArgs args) => dropController.Over(args);

    private void HandleDragLeave(object sender, DragEventArgs args) => dropController.Leave();

    private void HandleContentRouteDragEnter(object sender, DragEventArgs args) => dropController.EnterRoute(sender, args);

    private void HandleContentRouteDragOver(object sender, DragEventArgs args) => dropController.OverRoute(args);

    private void HandleContentRoutePickerDragOver(object sender, DragEventArgs args) => dropController.OverRoutePicker(args);

    private void HandleContentRoutePickerDragLeave(object sender, DragEventArgs args) => dropController.LeaveRoutePicker();

    private void HandleContentRouteDragLeave(object sender, DragEventArgs args) => dropController.LeaveRoute(sender);

    private void HandleContentRouteDrop(object sender, DragEventArgs args) => dropController.DropOnRoute(sender, args);

    private async void HandleDrop(object sender, DragEventArgs args) => await dropController.DropAsync(args);

    DispatcherQueue IDesktopIslandComponentHost.DispatcherQueue => DispatcherQueue;

    FrameworkElement IDesktopIslandComponentHost.RootElement => this;

    ContentControl IDesktopIslandComponentHost.CompactAssistantIndicator => CompactAssistantIndicator;

    IGlanceComponent? IDesktopIslandComponentHost.SelectedComponent => ViewModel.SelectedComponent;

    bool IDesktopIslandComponentHost.IsSelectedComponentVisible => IsLoaded && ViewModel.IsOpen && !ViewModel.IsLoadingModules && !ViewModel.IsTransientPresentationActive && !presentationController.IsAssistantRequested && !presentationController.IsContentRouteRequested && !presentationController.IsModuleReorderRequested;

    bool IDesktopIslandComponentHost.IsPinned => ViewModel.IsPinned;

    bool IDesktopIslandComponentHost.IsModuleReorderVisible => ViewModel.IsModuleReorderVisible;

    bool IDesktopIslandComponentHost.IsTransientExpansionLocked => ViewModel.IsTransientExpansionLocked;

    bool IDesktopIslandComponentHost.IsPointerWithinInteractiveRegion => IsPointerWithinInteractiveRegion;

    void IDesktopIslandComponentHost.SetAllowsActivation(bool value) => AllowsActivation = value;

    void IDesktopIslandComponentHost.SetExpansionLocked(bool value) => IsExpansionLocked = value;

    DispatcherQueue IDesktopIslandAnimationHost.DispatcherQueue => DispatcherQueue;

    DesktopIslandHostMode IDesktopIslandAnimationHost.HostMode => HostMode;

    bool IDesktopIslandAnimationHost.IsExpanded => ViewModel.IsExpanded;

    int IDesktopIslandAnimationHost.SelectedIndex => ViewModel.SelectedIndex;

    int IDesktopIslandAnimationHost.ComponentCount => ViewModel.ComponentCount;

    IGlanceComponent? IDesktopIslandAnimationHost.SelectedComponent => ViewModel.SelectedComponent;

    FrameworkElement IDesktopIslandAnimationHost.RootElement => this;

    FrameworkElement IDesktopIslandAnimationHost.CompactPresenter => CompactPresenter;

    FrameworkElement IDesktopIslandAnimationHost.ExpandedPresenter => ExpandedPresenter;

    Vector3 IDesktopIslandAnimationHost.GetTransitionOffset(bool isExpanded) => GetVisualStateTransitionOffset(isExpanded);

    TimeSpan IDesktopIslandAnimationHost.GetTransitionDuration(bool isExpanded) => GetVisualStateTransitionDuration(isExpanded);

    CompositionEasingFunction IDesktopIslandAnimationHost.CreateTransitionEasing() => CreateVisualStateTransitionEasing();

    DispatcherQueue IDesktopIslandModuleReorderHost.DispatcherQueue => DispatcherQueue;

    bool IDesktopIslandModuleReorderHost.IsLoaded => IsLoaded;

    bool IDesktopIslandModuleReorderHost.IsModuleReorderVisible => ViewModel.IsModuleReorderVisible;

    IGlanceComponent? IDesktopIslandModuleReorderHost.SelectedComponent => ViewModel.SelectedComponent;

    IList<IGlanceComponent> IDesktopIslandModuleReorderHost.ModuleOrder => ViewModel.ModuleOrder;

    ListView IDesktopIslandModuleReorderHost.ModuleReorderList => ModuleReorderList;

    FrameworkElement IDesktopIslandModuleReorderHost.ModuleReorderListClipHost => ModuleReorderListClipHost;

    FrameworkElement IDesktopIslandModuleReorderHost.ModuleReorderEdgeFadeHost => ModuleReorderEdgeFadeHost;

    Button IDesktopIslandModuleReorderHost.PreviousModuleOrderButton => PreviousModuleOrderButton;

    Button IDesktopIslandModuleReorderHost.NextModuleOrderButton => NextModuleOrderButton;

    double IDesktopIslandModuleReorderHost.ModuleReorderItemWidth => Resources["GlanceModuleReorderItemWidth"] is double width ? width : 164;

    DispatcherQueue IDesktopIslandDropHost.DispatcherQueue => DispatcherQueue;

    ScrollViewer IDesktopIslandDropHost.ContentRouteScrollViewer => ContentRouteScrollViewer;

    DesktopIslandHostMode IDesktopIslandDropHost.HostMode => HostMode;

    bool IDesktopIslandDropHost.IsModuleReorderVisible => ViewModel.IsModuleReorderVisible;

    bool IDesktopIslandDropHost.IsPinned => ViewModel.IsPinned;

    bool IDesktopIslandDropHost.IsExpanded => ViewModel.IsExpanded;

    bool IDesktopIslandDropHost.CanHandleContent(GlanceContentKind kind) => ViewModel.CanHandleContent(kind);

    bool IDesktopIslandDropHost.TryActivateContent(GlanceContentContext context, bool restoreExpandedState) => ViewModel.TryActivateContent(context, restoreExpandedState);

    bool IDesktopIslandDropHost.TryActivateContentRoute(string routeId) => ViewModel.TryActivateContentRoute(routeId);

    Task<bool> IDesktopIslandDropHost.HandleContentAsync(GlanceContentContext context) => ViewModel.HandleContentAsync(context);

    void IDesktopIslandDropHost.CompleteContentDrop()
    {
        ViewModel.CompleteContentRouting(true);
        StaysExpanded = true;
        DismissesOnOutsideClick = true;
        ApplyExpansionLock();
        Reveal();
    }

    void IDesktopIslandDropHost.CancelContentDrop()
    {
        ViewModel.EndContentPreview();
        Dismiss();
    }

    DispatcherQueue IDesktopIslandPresentationHost.DispatcherQueue => DispatcherQueue;

    DesktopIslandViewModel IDesktopIslandPresentationHost.ViewModel => ViewModel;

    IDesktopIslandBindings IDesktopIslandPresentationHost.BindingPolicy => BindingPolicy;

    bool IDesktopIslandPresentationHost.IsLoaded => IsLoaded;

    bool IDesktopIslandPresentationHost.StaysExpanded
    {
        get => StaysExpanded;
        set => StaysExpanded = value;
    }

    bool IDesktopIslandPresentationHost.DismissesOnOutsideClick
    {
        get => DismissesOnOutsideClick;
        set => DismissesOnOutsideClick = value;
    }

    object? IDesktopIslandPresentationHost.BackgroundContent
    {
        get => BackgroundContent;
        set => BackgroundContent = value;
    }

    FrameworkElement IDesktopIslandPresentationHost.CompactPresenter => CompactPresenter;

    FrameworkElement IDesktopIslandPresentationHost.ExpandedPresenter => ExpandedPresenter;

    FrameworkElement IDesktopIslandPresentationHost.TransientCompactPresenter => TransientCompactPresenter;

    FrameworkElement IDesktopIslandPresentationHost.TransientExpandedPresenter => TransientExpandedPresenter;

    FrameworkElement IDesktopIslandPresentationHost.CompactModuleLoadingView => CompactModuleLoadingView;

    FrameworkElement IDesktopIslandPresentationHost.ExpandedModuleLoadingView => ExpandedModuleLoadingView;

    ContentControl IDesktopIslandPresentationHost.CompactAssistantIndicator => CompactAssistantIndicator;

    ContentControl IDesktopIslandPresentationHost.ExpandedAssistantIndicator => ExpandedAssistantIndicator;

    FrameworkElement IDesktopIslandPresentationHost.Footer => Footer;

    FrameworkElement IDesktopIslandPresentationHost.ExpandedModuleSurface => ExpandedModuleSurface;

    ContentControl IDesktopIslandPresentationHost.AssistantOverlayPresenter => AssistantOverlayPresenter;

    FrameworkElement IDesktopIslandPresentationHost.ContentRoutePicker => ContentRoutePicker;

    FrameworkElement IDesktopIslandPresentationHost.ModuleReorderSurface => ModuleReorderSurface;

    FrameworkElement IDesktopIslandPresentationHost.ExpandedContentHost => ExpandedContentHost;

    FrameworkElement? IDesktopIslandPresentationHost.ContentTransitionClipHost => GetTemplateChild("PART_ShadowContainer") as FrameworkElement;

    FrameworkElement? IDesktopIslandPresentationHost.BackgroundElement => GetTemplateChild("PART_BackgroundContent") as FrameworkElement;

    FrameworkElement? IDesktopIslandPresentationHost.CompactTemplateContent => GetTemplateChild("PART_CompactContent") as FrameworkElement;

    object? IDesktopIslandPresentationHost.GetModuleBackgroundContent() => GetModuleBackgroundContent();

    void IDesktopIslandPresentationHost.UpdateLayout() => UpdateLayout();

    void IDesktopIslandPresentationHost.Reveal() => Reveal();

    void IDesktopIslandPresentationHost.Dismiss() => Dismiss();

}
