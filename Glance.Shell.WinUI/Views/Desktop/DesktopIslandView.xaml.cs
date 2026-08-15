using Elysium.Platform.Abstractions;
using Elysium.UI.Controls.WinUI;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;

namespace Glance.Shell.WinUI;

public sealed partial class DesktopIslandView :
    DesktopIsland
{
    private const string AssistantContinuumAnimationKey = "DesktopIsland.Assistant.Continuum";
    private const int AttentionExpansionDurationMs = 4000;
    private const int ContextualDragExitDelayMs = 160;
    private const int InteractionExitDelayMs = 240;
    private const int ModuleLoadingCrossFadeDurationMs = 360;
    private const float ModuleReorderEdgeFadeWidth = 32;
    private const double ModuleReorderSideItemMinimumOpacity = 0.68;
    private const int StartupAttentionDelayMs = 2500;

    private readonly DispatcherQueue dispatcherQueue;
    private readonly IMonitorLocator monitorLocator;
    private readonly ITaskbarLocator taskbarLocator;
    private DispatcherQueueTimer? attentionExpansionTimer;
    private DispatcherQueueTimer? contextualDragExitTimer;
    private DispatcherQueueTimer? interactionExitTimer;
    private DispatcherQueueTimer? startupAttentionTimer;
    private FrameworkElement? activeContentRouteTarget;
    private Button? pressedButton;
    private ListViewItem? draggedModuleOrderItem;
    private SoftwareBitmap? moduleOrderDragPreview;
    private ContainerVisual? moduleReorderEdgeFadeContainer;
    private CompositionLinearGradientBrush? moduleReorderLeftEdgeFadeGradient;
    private CompositionMaskBrush? moduleReorderLeftEdgeFadeMask;
    private CompositionSurfaceBrush? moduleReorderLeftEdgeFadeSourceBrush;
    private SpriteVisual? moduleReorderLeftEdgeFadeVisual;
    private CompositionVisualSurface? moduleReorderLeftEdgeSurface;
    private CompositionLinearGradientBrush? moduleReorderRightEdgeFadeGradient;
    private CompositionMaskBrush? moduleReorderRightEdgeFadeMask;
    private CompositionSurfaceBrush? moduleReorderRightEdgeFadeSourceBrush;
    private SpriteVisual? moduleReorderRightEdgeFadeVisual;
    private CompositionVisualSurface? moduleReorderRightEdgeSurface;
    private ScrollViewer? moduleReorderScrollViewer;
    private int moduleReorderCenteredIndex = -1;
    private int moduleReorderTargetIndex = -1;
    private string? droppedContentRouteId;
    private bool isContextualDragActive;
    private int contextualDragSession;
    private IGlanceIslandActivationComponent? islandActivationComponent;
    private IGlanceExpansionLockComponent? expansionLockComponent;
    private IGlanceInteractionAwareComponent? interactionComponent;
    private IGlanceFooterAppearanceComponent? footerAppearanceComponent;
    private bool isPointerOverIsland;
    private bool isAssistantPresentationRequested;
    private bool isContentRoutePresentationRequested;
    private bool isModuleReorderPresentationRequested;
    private int assistantPresentationTransition;
    private int contentRoutePresentationTransition;
    private int moduleReorderPresentationTransition;
    private int moduleLoadingTransition;
    private int transientPresentationTransition;
    private int previousIndex;
    private bool skipNextConnectedExpansion;

    public DesktopIslandView(IMonitorLocator monitorLocator,
        ITaskbarLocator taskbarLocator)
    {
        this.monitorLocator = monitorLocator;
        this.taskbarLocator = taskbarLocator;
        InitializeComponent();
        dispatcherQueue = DispatcherQueue;

        Opened += HandleIslandOpened;
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
        AddHandler(PointerPressedEvent, new PointerEventHandler(HandleButtonPointerPressed), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler(HandleButtonPointerReleased), true);
        AddHandler(PointerCanceledEvent, new PointerEventHandler(HandleButtonPointerCanceled), true);
        AddHandler(PointerCaptureLostEvent, new PointerEventHandler(HandleButtonPointerCaptureLost), true);
        ModuleReorderList.AddHandler(PointerWheelChangedEvent,
            new PointerEventHandler(HandleModuleReorderPointerWheelChanged),
            true);
    }

    public DesktopIslandViewModel ViewModel => (DesktopIslandViewModel)DataContext;

    public DesktopIslandPlacement ToPlacement(int index)
    {
        GlancePlacement placement = (GlancePlacement)index;

        return placement switch
        {
            GlancePlacement.Top => DesktopIslandPlacement.Top,
            GlancePlacement.Bottom => DesktopIslandPlacement.Bottom,
            _ => DesktopIslandPlacement.Top
        };
    }

    public DesktopIslandHostMode ToHostMode(int index) =>
        (GlanceDisplayLocation)index == GlanceDisplayLocation.Taskbar
            ? DesktopIslandHostMode.Taskbar
            : DesktopIslandHostMode.Floating;

    public Visibility WhenPinned(bool isPinned) => isPinned ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WhenNotPinned(bool isPinned) => isPinned ? Visibility.Collapsed : Visibility.Visible;

    public Visibility WhenOnScreenEdge(int index) => (GlanceDisplayLocation)index == GlanceDisplayLocation.DesktopEdge
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility WhenOnTaskbar(int index) => (GlanceDisplayLocation)index == GlanceDisplayLocation.Taskbar
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility WhenAvailable(bool isAvailable) => isAvailable ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WhenModulesLoaded(bool isLoadingModules) => isLoadingModules
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility WhenRoutePickerVisible(bool isVisible) => isVisible ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WhenRoutePickerHidden(bool isVisible) => isVisible ? Visibility.Collapsed : Visibility.Visible;

    public double ToCompactWidth(bool isAssistantAvailable,
        bool isAssistantEnabled,
        bool isLoadingModules,
        bool isTransientPresentationActive) => !isLoadingModules &&
            !isTransientPresentationActive &&
            isAssistantAvailable &&
            isAssistantEnabled
                ? 268
                : 228;

    public Visibility WhenAssistantAvailable(bool isAssistantAvailable,
        bool isAssistantEnabled,
        bool isLoadingModules,
        bool isTransientPresentationActive) => !isLoadingModules &&
        !isTransientPresentationActive &&
        isAssistantAvailable &&
        isAssistantEnabled
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility WhenPrimaryContentVisible(bool isLoadingModules,
        bool isTransientPresentationActive) => isLoadingModules || isTransientPresentationActive
        ? Visibility.Collapsed
        : Visibility.Visible;

    public object? ToBackgroundContent(IGlanceComponent? component,
        bool isLoadingModules,
        bool isTransientPresentationActive) => isLoadingModules || isTransientPresentationActive
            ? null
            : (component as IGlanceBackgroundComponent)?.BackgroundContent;

    private object? GetModuleBackgroundContent() => ToBackgroundContent(ViewModel.SelectedComponent,
        ViewModel.IsLoadingModules,
        ViewModel.IsTransientPresentationActive);

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        RefreshDisplayLocationIcons();
        previousIndex = ViewModel.SelectedIndex;
        ViewModel.PropertyChanged += HandleViewModelPropertyChanged;
        ViewModel.AttentionReceived += HandleAttentionReceived;
        ViewModel.Assistant.PropertyChanged += HandleAssistantPropertyChanged;
        ViewModel.Assistant.WakeWordDetected += HandleWakeWordDetected;
        ActualThemeChanged += HandleActualThemeChanged;
        (ViewModel.IntentService as GlanceIntentService)?.SetPresentationTargetProvider(GetIntentPresentationTarget);
        Deactivated += HandleIslandDeactivated;
        _ = DispatcherQueue.TryEnqueue(InitializeExpansionState);
        UpdateIslandActivationComponent();
        UpdateFooterAppearanceComponent();
        ApplyAssistantPresentation(ViewModel.Assistant.IsOverlayVisible);
        ApplyContentRoutePresentation(ViewModel.IsContentRoutePickerVisible);
        ApplyModuleReorderPresentation(ViewModel.IsModuleReorderVisible);
        InitializeModuleLoadingPresentation();
        StartStartupAttentionTimer();
        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, ApplyIslandActivationMode);
    }

    private void HandleIslandOpened(object sender,
        RoutedEventArgs args) => _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, ApplyIslandActivationMode);

    private void ApplyIslandActivationMode() =>
        AllowsActivation = islandActivationComponent?.RequiresIslandActivation == true;

    private void InitializeExpansionState()
    {
        ViewModel.IsExpanded = ViewModel.IsPinned;
        UpdateExpansionLockComponent();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        moduleLoadingTransition++;
        transientPresentationTransition++;
        ViewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        ViewModel.AttentionReceived -= HandleAttentionReceived;
        ViewModel.Assistant.PropertyChanged -= HandleAssistantPropertyChanged;
        ViewModel.Assistant.WakeWordDetected -= HandleWakeWordDetected;
        ActualThemeChanged -= HandleActualThemeChanged;
        (ViewModel.IntentService as GlanceIntentService)?.SetPresentationTargetProvider(null);
        Deactivated -= HandleIslandDeactivated;
        ReleasePressedButton();
        ClearIslandActivationComponent();
        ClearExpansionLockComponent();
        ClearFooterAppearanceComponent();
        EndComponentInteraction();
        StopAttentionExpansionTimer();
        StopContextualDragExitTimer();
        StopInteractionExitTimer();
        StopStartupAttentionTimer();
        DisposeModuleReorderEdgeFade();
        StaysExpanded = false;
        DismissesOnOutsideClick = false;
        assistantPresentationTransition++;
        moduleReorderPresentationTransition++;
    }

    private void InitializeModuleLoadingPresentation()
    {
        if (!IsLoaded)
        {
            return;
        }

        if (ViewModel.IsLoadingModules)
        {
            ShowModuleLoadingPresentation();
            return;
        }

        HideModuleLoadingPresentation();
    }

    private void ShowModuleLoadingPresentation()
    {
        moduleLoadingTransition++;
        CompactModuleLoadingView.Visibility = Visibility.Visible;
        ExpandedModuleLoadingView.Visibility = Visibility.Visible;
        FluentMotion.SetOpacity(CompactModuleLoadingView, 1);
        FluentMotion.SetOpacity(ExpandedModuleLoadingView, 1);
    }

    private void HideModuleLoadingPresentation()
    {
        CompactModuleLoadingView.Visibility = Visibility.Collapsed;
        ExpandedModuleLoadingView.Visibility = Visibility.Collapsed;
        FluentMotion.SetOpacity(CompactModuleLoadingView, 1);
        FluentMotion.SetOpacity(ExpandedModuleLoadingView, 1);
        FluentMotion.SetOpacity(CompactPresenter, 1);
        FluentMotion.SetOpacity(ExpandedPresenter, 1);
        FluentMotion.SetOpacity(TransientCompactPresenter, 1);
        FluentMotion.SetOpacity(TransientExpandedPresenter, 1);
        FluentMotion.SetOpacity(CompactAssistantIndicator, 1);
        FluentMotion.SetOpacity(Footer, 1);
        ApplyTransientPresentation(ViewModel.IsTransientPresentationActive);

        if (GetTemplateChild("PART_BackgroundContent") is FrameworkElement background)
        {
            FluentMotion.SetOpacity(background, 1);
        }
    }

    private void TransitionTransientPresentation(bool showTransient,
        bool allowLayoutRetry = true)
    {
        int transition = ++transientPresentationTransition;

        if (ViewModel.IsLoadingModules)
        {
            ApplyTransientPresentation(showTransient);

            if (!showTransient)
            {
                ViewModel.CompleteTransientPresentationDismissal();
            }

            return;
        }

        FrameworkElement outgoing = ViewModel.IsExpanded
            ? showTransient ? ExpandedPresenter : TransientExpandedPresenter
            : showTransient ? CompactPresenter : TransientCompactPresenter;
        FrameworkElement incoming = ViewModel.IsExpanded
            ? showTransient ? TransientExpandedPresenter : ExpandedPresenter
            : showTransient ? TransientCompactPresenter : CompactPresenter;

        outgoing.Visibility = Visibility.Visible;
        incoming.Visibility = Visibility.Visible;
        Footer.Visibility = Visibility.Collapsed;
        CompactAssistantIndicator.Visibility = Visibility.Collapsed;
        UpdateLayout();

        if (!IsInElementTree(outgoing) ||
            !IsInElementTree(incoming) ||
            outgoing.ActualWidth <= 0 ||
            outgoing.ActualHeight <= 0 ||
            incoming.ActualWidth <= 0 ||
            incoming.ActualHeight <= 0)
        {
            if (allowLayoutRetry)
            {
                _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                {
                    if (transition == transientPresentationTransition &&
                        showTransient == ViewModel.IsTransientPresentationActive)
                    {
                        TransitionTransientPresentation(showTransient, false);
                    }
                });
                return;
            }

            CompleteTransientPresentationTransition(transition, showTransient);
            return;
        }

        FluentMotion.PlayTransientPushTransition(outgoing,
            incoming,
            showTransient,
            () => CompleteTransientPresentationTransition(transition, showTransient));
    }

    private void CompleteTransientPresentationTransition(int transition,
        bool showTransient)
    {
        if (transition != transientPresentationTransition ||
            showTransient != ViewModel.IsTransientPresentationActive)
        {
            return;
        }

        ApplyTransientPresentation(showTransient);

        if (!showTransient)
        {
            ViewModel.CompleteTransientPresentationDismissal();
            BackgroundContent = GetModuleBackgroundContent();
        }
    }

    private void ApplyTransientPresentation(bool showTransient)
    {
        SetPresenterState(CompactPresenter, !showTransient);
        SetPresenterState(ExpandedPresenter, !showTransient);
        SetPresenterState(TransientCompactPresenter, showTransient);
        SetPresenterState(TransientExpandedPresenter, showTransient);
        Footer.Visibility = WhenPrimaryContentVisible(ViewModel.IsLoadingModules, showTransient);
        CompactAssistantIndicator.Visibility = WhenAssistantAvailable(ViewModel.Assistant.IsAvailable,
            ViewModel.Assistant.IsEnabled,
            ViewModel.IsLoadingModules,
            showTransient);
    }

    private static void SetPresenterState(FrameworkElement presenter,
        bool isVisible)
    {
        if (isVisible)
        {
            presenter.Visibility = Visibility.Visible;
        }

        FluentMotion.SetContentPresentationState(presenter, isVisible);

        if (!isVisible)
        {
            presenter.Visibility = Visibility.Collapsed;
        }
    }

    private void PlayModuleLoadingCompletionTransition(int transition)
    {
        if (!IsLoaded || transition != moduleLoadingTransition)
        {
            return;
        }

        CompactModuleLoadingView.Visibility = Visibility.Visible;
        ExpandedModuleLoadingView.Visibility = Visibility.Visible;
        bool isTransientPresentationActive = ViewModel.IsTransientPresentationActive;
        FrameworkElement compactIncoming = isTransientPresentationActive ? TransientCompactPresenter : CompactPresenter;
        FrameworkElement expandedIncoming = isTransientPresentationActive ? TransientExpandedPresenter : ExpandedPresenter;
        compactIncoming.Visibility = Visibility.Visible;
        expandedIncoming.Visibility = Visibility.Visible;

        if (!isTransientPresentationActive)
        {
            Footer.Visibility = Visibility.Visible;
        }

        UpdateLayout();

        FrameworkElement? background = GetTemplateChild("PART_BackgroundContent") as FrameworkElement;
        Visual compactLoadingVisual = ElementCompositionPreview.GetElementVisual(CompactModuleLoadingView);
        Visual expandedLoadingVisual = ElementCompositionPreview.GetElementVisual(ExpandedModuleLoadingView);
        Visual compactPresenterVisual = ElementCompositionPreview.GetElementVisual(compactIncoming);
        Visual expandedPresenterVisual = ElementCompositionPreview.GetElementVisual(expandedIncoming);
        Visual assistantIndicatorVisual = ElementCompositionPreview.GetElementVisual(CompactAssistantIndicator);
        Visual footerVisual = ElementCompositionPreview.GetElementVisual(Footer);
        Visual? backgroundVisual = background is null ? null : ElementCompositionPreview.GetElementVisual(background);
        Compositor compositor = compactLoadingVisual.Compositor;
        CubicBezierEasingFunction entranceEasing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1));
        CubicBezierEasingFunction exitEasing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.7f, 0), new Vector2(1, 0.5f));
        TimeSpan duration = TimeSpan.FromMilliseconds(ModuleLoadingCrossFadeDurationMs);

        SetOpacity(compactLoadingVisual, 1);
        SetOpacity(expandedLoadingVisual, 1);
        SetOpacity(compactPresenterVisual, 0);
        SetOpacity(expandedPresenterVisual, 0);
        if (!isTransientPresentationActive)
        {
            SetOpacity(assistantIndicatorVisual, 0);
            SetOpacity(footerVisual, 0);
        }

        if (backgroundVisual is not null)
        {
            SetOpacity(backgroundVisual, 0);
        }

        CompositionScopedBatch batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        StartOpacityAnimation(compositor, compactLoadingVisual, 1, 0, duration, exitEasing);
        StartOpacityAnimation(compositor, expandedLoadingVisual, 1, 0, duration, exitEasing);
        StartOpacityAnimation(compositor, compactPresenterVisual, 0, 1, duration, entranceEasing);
        StartOpacityAnimation(compositor, expandedPresenterVisual, 0, 1, duration, entranceEasing);
        if (!isTransientPresentationActive)
        {
            StartOpacityAnimation(compositor, assistantIndicatorVisual, 0, 1, duration, entranceEasing);
            StartOpacityAnimation(compositor, footerVisual, 0, 1, duration, entranceEasing);
        }

        if (backgroundVisual is not null)
        {
            StartOpacityAnimation(compositor, backgroundVisual, 0, 1, duration, entranceEasing);
        }

        batch.Completed += (_, _) => DispatcherQueue.TryEnqueue(() =>
        {
            if (transition == moduleLoadingTransition && !ViewModel.IsLoadingModules)
            {
                HideModuleLoadingPresentation();
            }
        });
        batch.End();
    }

    private static void StartOpacityAnimation(Compositor compositor,
        Visual visual,
        float from,
        float to,
        TimeSpan duration,
        CompositionEasingFunction easing)
    {
        ScalarKeyFrameAnimation animation = compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, easing);
        animation.Duration = duration;
        visual.StartAnimation(nameof(Visual.Opacity), animation);
    }

    private static void SetOpacity(Visual visual,
        float opacity)
    {
        visual.StopAnimation(nameof(Visual.Opacity));
        visual.Opacity = opacity;
    }

    private void StartStartupAttentionTimer()
    {
        startupAttentionTimer ??= CreateStartupAttentionTimer();
        startupAttentionTimer.Stop();
        startupAttentionTimer.Start();
    }

    private DispatcherQueueTimer CreateStartupAttentionTimer()
    {
        DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(StartupAttentionDelayMs);
        timer.IsRepeating = false;
        timer.Tick += HandleStartupAttentionTimerTick;
        return timer;
    }

    private void StopStartupAttentionTimer() => startupAttentionTimer?.Stop();

    private void HandleStartupAttentionTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        ViewModel.CompleteStartup();
    }

    private void HandleAttentionReceived(object? sender, GlanceAttentionRequest request) => _ = DispatcherQueue.TryEnqueue(() =>
                                                                                                 {
                                                                                                     Reveal();

                                                                                                     if (request.Expand)
                                                                                                     {
                                                                                                         StartAttentionExpansionTimer();
                                                                                                     }

                                                                                                     FrameworkElement presenter = ViewModel.IsExpanded
                                                                                                         ? ExpandedPresenter
                                                                                                         : CompactPresenter;

                                                                                                     FluentMotion.PlayPulse(presenter);
                                                                                                 });

    private void HandleWakeWordDetected(object? sender, EventArgs args) => _ = DispatcherQueue.TryEnqueue(() =>
    {
        if (ViewModel.IsModuleReorderVisible)
        {
            ViewModel.CancelModuleReorder();
            _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, ShowAssistantPresentation);
            return;
        }

        ShowAssistantPresentation();
    });

    private void HandleAssistantPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(IGlanceAssistantService.IsResultPresentationActive))
        {
            _ = DispatcherQueue.TryEnqueue(UpdateAssistantDismissalState);
            return;
        }

        if (args.PropertyName != nameof(IGlanceAssistantService.IsOverlayVisible))
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            UpdateAssistantDismissalState();

            if (ViewModel.Assistant.IsOverlayVisible)
            {
                ShowAssistantPresentation();
                return;
            }

            HideAssistantPresentation();
        });
    }

    private void ShowAssistantPresentation()
    {
        if (isAssistantPresentationRequested || !ViewModel.Assistant.IsOverlayVisible)
        {
            return;
        }

        isAssistantPresentationRequested = true;
        PrepareAssistantContinuumAnimation(true);
        StaysExpanded = true;
        UpdateAssistantDismissalState();
        ApplyExpansionLock();
        Reveal();
        ViewModel.IsExpanded = true;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (isAssistantPresentationRequested)
            {
                TransitionAssistantPresentation(true);
            }
        });
    }

    private void HideAssistantPresentation()
    {
        if (!isAssistantPresentationRequested)
        {
            return;
        }

        PrepareAssistantContinuumAnimation(false);
        isAssistantPresentationRequested = false;
        TransitionAssistantPresentation(false);
    }

    private void TransitionAssistantPresentation(bool showAssistant, bool allowLayoutRetry = true)
    {
        if (showAssistant != isAssistantPresentationRequested)
        {
            return;
        }

        int transition = ++assistantPresentationTransition;
        FrameworkElement outgoing = showAssistant ? ExpandedModuleSurface : AssistantOverlayPresenter;
        FrameworkElement incoming = showAssistant ? AssistantOverlayPresenter : ExpandedModuleSurface;
        outgoing.Visibility = Visibility.Visible;
        incoming.Visibility = Visibility.Visible;
        UpdateLayout();

        if (!showAssistant)
        {
            BackgroundContent = GetModuleBackgroundContent();
        }

        if (!IsInElementTree(outgoing) ||
            !IsInElementTree(incoming) ||
            outgoing.ActualWidth <= 0 ||
            outgoing.ActualHeight <= 0 ||
            incoming.ActualWidth <= 0 ||
            incoming.ActualHeight <= 0)
        {
            if (allowLayoutRetry)
            {
                _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                {
                    if (showAssistant == isAssistantPresentationRequested)
                    {
                        TransitionAssistantPresentation(showAssistant, false);
                    }
                });
                return;
            }

            ApplyAssistantPresentation(showAssistant);

            if (!showAssistant)
            {
                CompleteAssistantPresentationExit();
            }

            return;
        }

        outgoing.IsHitTestVisible = false;
        incoming.IsHitTestVisible = false;
        StartAssistantContinuumAnimation(showAssistant);
        FrameworkElement? background = GetTemplateChild("PART_BackgroundContent") as FrameworkElement;
        FluentMotion.PlayConnectedContentTransition(outgoing,
            incoming,
            background,
            showAssistant,
            () =>
        {
            if (transition != assistantPresentationTransition)
            {
                return;
            }

            ApplyAssistantPresentation(showAssistant);
            if (background is not null)
            {
                FluentMotion.SetOpacity(background, 1);
            }

            if (!showAssistant)
            {
                CompleteAssistantPresentationExit();
            }
        });
    }

    private void ApplyAssistantPresentation(bool showAssistant)
    {
        isAssistantPresentationRequested = showAssistant;

        if (showAssistant)
        {
            StaysExpanded = true;
            UpdateAssistantDismissalState();
            ApplyExpansionLock();
        }

        FluentMotion.SetContentPresentationState(ExpandedModuleSurface, !showAssistant);
        FluentMotion.SetContentPresentationState(AssistantOverlayPresenter, showAssistant);
        ExpandedModuleSurface.Visibility = showAssistant ? Visibility.Collapsed : Visibility.Visible;
        AssistantOverlayPresenter.Visibility = showAssistant ? Visibility.Visible : Visibility.Collapsed;
        BackgroundContent = showAssistant ? null : GetModuleBackgroundContent();
    }

    private void CompleteAssistantPresentationExit()
    {
        ViewModel.IsExpanded = true;
        ApplyExpansionLock();
    }

    private void PrepareAssistantContinuumAnimation(bool showAssistant)
    {
        FrameworkElement? source = showAssistant ? GetAssistantIndicatorAnimationElement() : GetAssistantOverlayAnimationElement();

        if (source is null || !IsInElementTree(source))
        {
            return;
        }

        ConnectedAnimationService animationService = ConnectedAnimationService.GetForCurrentView();

        try
        {
            ConnectedAnimation animation = animationService.PrepareToAnimate(AssistantContinuumAnimationKey, source);
            animation.Configuration = showAssistant ?
                new GravityConnectedAnimationConfiguration() :
                new DirectConnectedAnimationConfiguration();
        }
        catch (ArgumentException)
        {
        }
    }

    private void StartAssistantContinuumAnimation(bool showAssistant)
    {
        FrameworkElement? destination = showAssistant ? GetAssistantOverlayAnimationElement() : GetAssistantIndicatorAnimationElement();
        ConnectedAnimation? animation = ConnectedAnimationService.GetForCurrentView().GetAnimation(AssistantContinuumAnimationKey);

        if (animation is null || destination is null || !IsInElementTree(destination))
        {
            animation?.Cancel();
            return;
        }

        try
        {
            _ = animation.TryStart(destination);
        }
        catch (ArgumentException)
        {
            animation.Cancel();
        }
    }

    private FrameworkElement? GetAssistantIndicatorAnimationElement()
    {
        ContentControl indicator = ViewModel.IsExpanded ? ExpandedAssistantIndicator : CompactAssistantIndicator;
        return (indicator.Content as IGlanceAssistantConnectedAnimationView)?.ConnectedAnimationElement as FrameworkElement;
    }

    private FrameworkElement? GetAssistantOverlayAnimationElement() => (AssistantOverlayPresenter.Content as IGlanceAssistantConnectedAnimationView)?.ConnectedAnimationElement as FrameworkElement;

    private void ShowContentRoutePresentation()
    {
        if (isContentRoutePresentationRequested || !ViewModel.IsContentRoutePickerVisible)
        {
            return;
        }

        isContentRoutePresentationRequested = true;
        Reveal();
        ViewModel.IsExpanded = true;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (isContentRoutePresentationRequested)
            {
                TransitionContentRoutePresentation(true);
            }
        });
    }

    private void HideContentRoutePresentation()
    {
        if (!isContentRoutePresentationRequested)
        {
            return;
        }

        isContentRoutePresentationRequested = false;
        TransitionContentRoutePresentation(false);
    }

    private void TransitionContentRoutePresentation(bool showRoutes,
        bool allowLayoutRetry = true)
    {
        if (showRoutes != isContentRoutePresentationRequested)
        {
            return;
        }

        int transition = ++contentRoutePresentationTransition;
        FrameworkElement outgoing = showRoutes ? ExpandedModuleSurface : ContentRoutePicker;
        FrameworkElement incoming = showRoutes ? ContentRoutePicker : ExpandedModuleSurface;
        outgoing.Visibility = Visibility.Visible;
        incoming.Visibility = Visibility.Visible;
        UpdateLayout();

        if (!showRoutes)
        {
            BackgroundContent = GetModuleBackgroundContent();
        }

        if (!IsInElementTree(outgoing) ||
            !IsInElementTree(incoming) ||
            outgoing.ActualWidth <= 0 ||
            outgoing.ActualHeight <= 0 ||
            incoming.ActualWidth <= 0 ||
            incoming.ActualHeight <= 0)
        {
            if (allowLayoutRetry)
            {
                _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                {
                    if (showRoutes == isContentRoutePresentationRequested)
                    {
                        TransitionContentRoutePresentation(showRoutes, false);
                    }
                });
                return;
            }

            ApplyContentRoutePresentation(showRoutes);
            return;
        }

        outgoing.IsHitTestVisible = false;
        incoming.IsHitTestVisible = showRoutes;
        ExpandedContentHost.Clip = new RectangleGeometry
        {
            Rect = new Rect(0, 0, ExpandedContentHost.ActualWidth, ExpandedContentHost.ActualHeight)
        };
        FrameworkElement? background = GetTemplateChild("PART_BackgroundContent") as FrameworkElement;
        FrameworkElement? compactContent = showRoutes ?
            GetTemplateChild("PART_CompactContent") as FrameworkElement :
            null;

        FluentMotion.PlayVerticalPushTransition(outgoing,
            incoming,
            background,
            compactContent,
            showRoutes,
            () =>
        {
            if (transition != contentRoutePresentationTransition)
            {
                return;
            }

            ApplyContentRoutePresentation(showRoutes);

            if (background is not null)
            {
                FluentMotion.SetContentPresentationState(background, !showRoutes);
            }

            if (compactContent is not null)
            {
                FluentMotion.ResetTranslation(compactContent);
            }
        });
    }

    private void ApplyContentRoutePresentation(bool showRoutes)
    {
        ExpandedContentHost.Clip = null;

        if (!showRoutes)
        {
            ReleaseActiveContentRouteTarget();
        }

        isContentRoutePresentationRequested = showRoutes;
        FluentMotion.SetContentPresentationState(ExpandedModuleSurface, !showRoutes);
        FluentMotion.SetContentPresentationState(ContentRoutePicker, showRoutes);
        ExpandedModuleSurface.Visibility = showRoutes ? Visibility.Collapsed : Visibility.Visible;
        ContentRoutePicker.Visibility = showRoutes ? Visibility.Visible : Visibility.Collapsed;
        BackgroundContent = showRoutes ? null : GetModuleBackgroundContent();
    }

    private void ShowModuleReorderPresentation()
    {
        if (isModuleReorderPresentationRequested || !ViewModel.IsModuleReorderVisible)
        {
            return;
        }

        isModuleReorderPresentationRequested = true;
        StaysExpanded = true;
        DismissesOnOutsideClick = false;
        ApplyExpansionLock();
        Reveal();
        ViewModel.IsExpanded = true;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (isModuleReorderPresentationRequested)
            {
                TransitionModuleReorderPresentation(true);
            }
        });
    }

    private void HideModuleReorderPresentation()
    {
        if (!isModuleReorderPresentationRequested)
        {
            return;
        }

        isModuleReorderPresentationRequested = false;
        TransitionModuleReorderPresentation(false);
    }

    private void TransitionModuleReorderPresentation(bool showReorder,
        bool allowLayoutRetry = true)
    {
        if (showReorder != isModuleReorderPresentationRequested)
        {
            return;
        }

        int transition = ++moduleReorderPresentationTransition;
        FrameworkElement outgoing = showReorder ? ExpandedModuleSurface : ModuleReorderSurface;
        FrameworkElement incoming = showReorder ? ModuleReorderSurface : ExpandedModuleSurface;
        outgoing.Visibility = Visibility.Visible;
        incoming.Visibility = Visibility.Visible;

        if (!showReorder)
        {
            BackgroundContent = GetModuleBackgroundContent();
        }

        UpdateLayout();

        if (showReorder && ViewModel.SelectedComponent is not null)
        {
            CenterSelectedModuleInReorderList();
        }

        if (!IsInElementTree(outgoing) ||
            !IsInElementTree(incoming) ||
            outgoing.ActualWidth <= 0 ||
            outgoing.ActualHeight <= 0 ||
            incoming.ActualWidth <= 0 ||
            incoming.ActualHeight <= 0)
        {
            if (allowLayoutRetry)
            {
                _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                {
                    if (showReorder == isModuleReorderPresentationRequested)
                    {
                        TransitionModuleReorderPresentation(showReorder, false);
                    }
                });
                return;
            }

            ApplyModuleReorderPresentation(showReorder);

            if (!showReorder)
            {
                CompleteModuleReorderPresentationExit();
            }

            return;
        }

        outgoing.IsHitTestVisible = false;
        incoming.IsHitTestVisible = false;
        FrameworkElement? background = GetTemplateChild("PART_BackgroundContent") as FrameworkElement;
        FluentMotion.PlaySemanticZoomTransition(outgoing,
            incoming,
            background,
            showReorder,
            () =>
        {
            if (transition != moduleReorderPresentationTransition)
            {
                return;
            }

            ApplyModuleReorderPresentation(showReorder);

            if (!showReorder)
            {
                CompleteModuleReorderPresentationExit();
            }
        });
    }

    private void CenterSelectedModuleInReorderList()
    {
        IGlanceComponent? selectedComponent = ViewModel.SelectedComponent;

        if (selectedComponent is null || ModuleReorderList.ActualWidth <= 0)
        {
            return;
        }

        double itemWidth = Resources["GlanceModuleReorderItemWidth"] is double width ?
            width :
            164;
        double edgePadding = Math.Max(0,
            (ModuleReorderList.ActualWidth - itemWidth) / 2);
        ModuleReorderList.Padding = new Thickness(edgePadding, 0, edgePadding, 0);
        ModuleReorderList.UpdateLayout();
        CenterModuleOrderItem(ViewModel.ModuleOrder.IndexOf(selectedComponent),
            true);
    }

    private void HandleModuleReorderListLoaded(object sender,
        RoutedEventArgs args)
    {
        UpdateModuleReorderEdgeFade();
        ScrollViewer? scrollViewer = GetModuleReorderScrollViewer();

        if (scrollViewer is null)
        {
            _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                UpdateModuleReorderScrollButtons(GetModuleReorderScrollViewer()));
        }
    }

    private void HandleModuleReorderEdgeFadeHostSizeChanged(object sender,
        SizeChangedEventArgs args) => UpdateModuleReorderEdgeFade();

    private void UpdateModuleReorderEdgeFade()
    {
        float width = (float)ModuleReorderEdgeFadeHost.ActualWidth;
        float height = (float)ModuleReorderEdgeFadeHost.ActualHeight;

        if (!IsLoaded || width <= 0 || height <= 0)
        {
            return;
        }

        float fadeWidth = Math.Min(ModuleReorderEdgeFadeWidth, width / 2);
        Visual sourceVisual = ElementCompositionPreview.GetElementVisual(ModuleReorderList);
        Compositor compositor = sourceVisual.Compositor;

        if (moduleReorderEdgeFadeContainer is null)
        {
            CreateModuleReorderEdgeFade(compositor, sourceVisual);
        }

        Visual clipHostVisual = ElementCompositionPreview.GetElementVisual(ModuleReorderListClipHost);
        InsetClip clip = compositor.CreateInsetClip();
        clip.LeftInset = fadeWidth;
        clip.RightInset = fadeWidth;
        clipHostVisual.Clip = clip;

        moduleReorderEdgeFadeContainer!.Size = new Vector2(width, height);
        moduleReorderLeftEdgeSurface!.SourceSize = new Vector2(fadeWidth, height);
        moduleReorderLeftEdgeSurface.SourceOffset = Vector2.Zero;
        moduleReorderLeftEdgeFadeVisual!.Size = new Vector2(fadeWidth, height);
        moduleReorderLeftEdgeFadeVisual.Offset = Vector3.Zero;
        moduleReorderRightEdgeSurface!.SourceSize = new Vector2(fadeWidth, height);
        moduleReorderRightEdgeSurface.SourceOffset = new Vector2(width - fadeWidth, 0);
        moduleReorderRightEdgeFadeVisual!.Size = new Vector2(fadeWidth, height);
        moduleReorderRightEdgeFadeVisual.Offset = new Vector3(width - fadeWidth, 0, 0);
    }

    private void CreateModuleReorderEdgeFade(Compositor compositor,
        Visual sourceVisual)
    {
        moduleReorderLeftEdgeSurface = compositor.CreateVisualSurface();
        moduleReorderLeftEdgeSurface.SourceVisual = sourceVisual;
        moduleReorderLeftEdgeFadeSourceBrush = compositor.CreateSurfaceBrush(moduleReorderLeftEdgeSurface);
        moduleReorderLeftEdgeFadeGradient = CreateModuleReorderEdgeFadeGradient(compositor, true);
        moduleReorderLeftEdgeFadeMask = compositor.CreateMaskBrush();
        moduleReorderLeftEdgeFadeMask.Source = moduleReorderLeftEdgeFadeSourceBrush;
        moduleReorderLeftEdgeFadeMask.Mask = moduleReorderLeftEdgeFadeGradient;
        moduleReorderLeftEdgeFadeVisual = compositor.CreateSpriteVisual();
        moduleReorderLeftEdgeFadeVisual.Brush = moduleReorderLeftEdgeFadeMask;

        moduleReorderRightEdgeSurface = compositor.CreateVisualSurface();
        moduleReorderRightEdgeSurface.SourceVisual = sourceVisual;
        moduleReorderRightEdgeFadeSourceBrush = compositor.CreateSurfaceBrush(moduleReorderRightEdgeSurface);
        moduleReorderRightEdgeFadeGradient = CreateModuleReorderEdgeFadeGradient(compositor, false);
        moduleReorderRightEdgeFadeMask = compositor.CreateMaskBrush();
        moduleReorderRightEdgeFadeMask.Source = moduleReorderRightEdgeFadeSourceBrush;
        moduleReorderRightEdgeFadeMask.Mask = moduleReorderRightEdgeFadeGradient;
        moduleReorderRightEdgeFadeVisual = compositor.CreateSpriteVisual();
        moduleReorderRightEdgeFadeVisual.Brush = moduleReorderRightEdgeFadeMask;

        moduleReorderEdgeFadeContainer = compositor.CreateContainerVisual();
        moduleReorderEdgeFadeContainer.Children.InsertAtTop(moduleReorderLeftEdgeFadeVisual);
        moduleReorderEdgeFadeContainer.Children.InsertAtTop(moduleReorderRightEdgeFadeVisual);
        ElementCompositionPreview.SetElementChildVisual(ModuleReorderEdgeFadeHost,
            moduleReorderEdgeFadeContainer);
    }

    private static CompositionLinearGradientBrush CreateModuleReorderEdgeFadeGradient(Compositor compositor,
        bool isLeftEdge)
    {
        CompositionLinearGradientBrush gradient = compositor.CreateLinearGradientBrush();
        gradient.StartPoint = Vector2.Zero;
        gradient.EndPoint = Vector2.UnitX;
        gradient.ColorStops.Add(compositor.CreateColorGradientStop(0,
            isLeftEdge ? Colors.Transparent : Colors.White));
        gradient.ColorStops.Add(compositor.CreateColorGradientStop(1,
            isLeftEdge ? Colors.White : Colors.Transparent));
        return gradient;
    }

    private void DisposeModuleReorderEdgeFade()
    {
        ElementCompositionPreview.GetElementVisual(ModuleReorderListClipHost).Clip = null;
        ElementCompositionPreview.SetElementChildVisual(ModuleReorderEdgeFadeHost, null);
        moduleReorderLeftEdgeFadeGradient?.Dispose();
        moduleReorderRightEdgeFadeGradient?.Dispose();
        moduleReorderLeftEdgeFadeMask?.Dispose();
        moduleReorderRightEdgeFadeMask?.Dispose();
        moduleReorderLeftEdgeFadeSourceBrush?.Dispose();
        moduleReorderRightEdgeFadeSourceBrush?.Dispose();
        moduleReorderLeftEdgeFadeVisual?.Dispose();
        moduleReorderRightEdgeFadeVisual?.Dispose();
        moduleReorderLeftEdgeSurface?.Dispose();
        moduleReorderRightEdgeSurface?.Dispose();
        moduleReorderEdgeFadeContainer?.Dispose();
        moduleReorderLeftEdgeFadeGradient = null;
        moduleReorderRightEdgeFadeGradient = null;
        moduleReorderLeftEdgeFadeMask = null;
        moduleReorderRightEdgeFadeMask = null;
        moduleReorderLeftEdgeFadeSourceBrush = null;
        moduleReorderRightEdgeFadeSourceBrush = null;
        moduleReorderLeftEdgeFadeVisual = null;
        moduleReorderRightEdgeFadeVisual = null;
        moduleReorderLeftEdgeSurface = null;
        moduleReorderRightEdgeSurface = null;
        moduleReorderEdgeFadeContainer = null;
    }

    private ScrollViewer? GetModuleReorderScrollViewer()
    {
        ScrollViewer? scrollViewer =
            FindVisualDescendant<ScrollViewer>(ModuleReorderList);

        if (scrollViewer is null)
        {
            return null;
        }

        AttachModuleReorderScrollViewer(scrollViewer);
        return scrollViewer;
    }

    private void AttachModuleReorderScrollViewer(ScrollViewer scrollViewer)
    {
        if (ReferenceEquals(moduleReorderScrollViewer, scrollViewer))
        {
            UpdateModuleReorderScrollButtons(scrollViewer);
            UpdateModuleReorderItemFade(scrollViewer);
            return;
        }

        if (moduleReorderScrollViewer is not null)
        {
            moduleReorderScrollViewer.ViewChanged -= HandleModuleReorderViewChanged;
        }

        moduleReorderScrollViewer = scrollViewer;
        scrollViewer.ViewChanged += HandleModuleReorderViewChanged;
        UpdateModuleReorderScrollButtons(scrollViewer);
        UpdateModuleReorderItemFade(scrollViewer);
    }

    private void HandleModuleReorderViewChanged(object? sender,
        ScrollViewerViewChangedEventArgs args)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        moduleReorderCenteredIndex = GetNearestModuleOrderIndex(scrollViewer);
        UpdateModuleReorderScrollButtons(scrollViewer);
        UpdateModuleReorderItemFade(scrollViewer);

        if (args.IsIntermediate)
        {
            return;
        }

        moduleReorderTargetIndex = -1;
        double targetOffset = GetModuleOrderOffset(moduleReorderCenteredIndex,
            scrollViewer);

        if (Math.Abs(scrollViewer.HorizontalOffset - targetOffset) > 0.5)
        {
            CenterModuleOrderItem(moduleReorderCenteredIndex, false);
        }
    }

    private void HandleModuleReorderPointerWheelChanged(object sender,
        PointerRoutedEventArgs args)
    {
        if (!ViewModel.IsModuleReorderVisible)
        {
            return;
        }

        int delta = args.GetCurrentPoint(ModuleReorderList)
            .Properties.MouseWheelDelta;

        if (delta == 0)
        {
            return;
        }

        ScrollModuleOrder(delta < 0 ? 1 : -1);
        args.Handled = true;
    }

    private void HandlePreviousModuleOrderClicked(object sender,
        RoutedEventArgs args) => ScrollModuleOrder(-1);

    private void HandleNextModuleOrderClicked(object sender,
        RoutedEventArgs args) => ScrollModuleOrder(1);

    private void ScrollModuleOrder(int direction)
    {
        ScrollViewer? scrollViewer = GetModuleReorderScrollViewer();

        if (scrollViewer is null)
        {
            return;
        }

        int currentIndex = moduleReorderTargetIndex >= 0 ?
            moduleReorderTargetIndex :
            moduleReorderCenteredIndex >= 0 ?
            moduleReorderCenteredIndex :
            GetNearestModuleOrderIndex(scrollViewer);
        CenterModuleOrderItem(currentIndex + direction, false);
    }

    private void UpdateModuleReorderScrollButtons(ScrollViewer? scrollViewer)
    {
        int itemCount = ViewModel.ModuleOrder.Count;
        int centeredIndex = moduleReorderTargetIndex >= 0 ?
            moduleReorderTargetIndex :
            scrollViewer is null ?
            moduleReorderCenteredIndex :
            GetNearestModuleOrderIndex(scrollViewer);
        PreviousModuleOrderButton.IsEnabled = itemCount > 1 && centeredIndex > 0;
        NextModuleOrderButton.IsEnabled = itemCount > 1 &&
            centeredIndex < itemCount - 1;
    }

    private void CenterModuleOrderItem(int index,
        bool disableAnimation)
    {
        ScrollViewer? scrollViewer = GetModuleReorderScrollViewer();
        int itemCount = ViewModel.ModuleOrder.Count;

        if (scrollViewer is null || itemCount == 0)
        {
            return;
        }

        moduleReorderCenteredIndex = Math.Clamp(index, 0, itemCount - 1);
        moduleReorderTargetIndex = disableAnimation ?
            -1 :
            moduleReorderCenteredIndex;
        double targetOffset = GetModuleOrderOffset(moduleReorderCenteredIndex,
            scrollViewer);
        _ = scrollViewer.ChangeView(targetOffset,
            null,
            null,
            disableAnimation);
        UpdateModuleReorderScrollButtons(scrollViewer);
        UpdateModuleReorderItemFade(scrollViewer);
    }

    private int GetNearestModuleOrderIndex(ScrollViewer scrollViewer)
    {
        int itemCount = ViewModel.ModuleOrder.Count;

        if (itemCount == 0)
        {
            return -1;
        }

        double stride = GetModuleOrderItemStride();
        int index = (int)Math.Round(scrollViewer.HorizontalOffset / stride,
            MidpointRounding.AwayFromZero);
        return Math.Clamp(index, 0, itemCount - 1);
    }

    private double GetModuleOrderOffset(int index,
        ScrollViewer scrollViewer) => Math.Clamp(index * GetModuleOrderItemStride(),
            0,
            scrollViewer.ScrollableWidth);

    private double GetModuleOrderItemStride()
    {
        double itemWidth = Resources["GlanceModuleReorderItemWidth"] is double width ?
            width :
            164;
        return itemWidth + 2;
    }

    private void UpdateModuleReorderItemFade(ScrollViewer? scrollViewer)
    {
        if (scrollViewer is null || scrollViewer.ViewportWidth <= 0)
        {
            return;
        }

        double viewportCenter = scrollViewer.ViewportWidth / 2;
        double fadeStart = GetModuleOrderItemStride() * 0.42;
        double fadeRange = Math.Max(1, viewportCenter - fadeStart);

        foreach (IGlanceComponent component in ViewModel.ModuleOrder)
        {
            if (ModuleReorderList.ContainerFromItem(component) is not ListViewItem item)
            {
                continue;
            }

            if (ReferenceEquals(item, draggedModuleOrderItem))
            {
                item.Opacity = 1;
                continue;
            }

            GeneralTransform transform = item.TransformToVisual(scrollViewer);
            Windows.Foundation.Point origin =
                transform.TransformPoint(new Windows.Foundation.Point());
            double itemCenter = origin.X + (item.ActualWidth / 2);
            double distance = Math.Abs(itemCenter - viewportCenter);
            double progress = Math.Clamp((distance - fadeStart) / fadeRange,
                0,
                1);
            item.Opacity = 1 - (progress * (1 - ModuleReorderSideItemMinimumOpacity));
        }
    }

    private void ApplyModuleReorderPresentation(bool showReorder)
    {
        isModuleReorderPresentationRequested = showReorder;

        if (showReorder)
        {
            StaysExpanded = true;
            DismissesOnOutsideClick = false;
            ApplyExpansionLock();
        }

        FluentMotion.SetContentPresentationState(ExpandedModuleSurface, !showReorder);
        FluentMotion.SetContentPresentationState(ModuleReorderSurface, showReorder);
        ExpandedModuleSurface.Visibility = showReorder ? Visibility.Collapsed : Visibility.Visible;
        ModuleReorderSurface.Visibility = showReorder ? Visibility.Visible : Visibility.Collapsed;
        BackgroundContent = showReorder ? null : GetModuleBackgroundContent();
    }

    private void CompleteModuleReorderPresentationExit()
    {
        StaysExpanded = false;
        DismissesOnOutsideClick = false;
        ViewModel.IsExpanded = true;
        ApplyExpansionLock();
    }

    private GlanceScreenRectangle? GetIntentPresentationTarget()
    {
        if (!GetWindowRect(Handle, out NativeRect windowBounds))
        {
            return null;
        }

        int width = Math.Max(1, windowBounds.Right - windowBounds.Left);
        int height = Math.Max(1, windowBounds.Bottom - windowBounds.Top);
        const int targetWidth = 64;
        const int targetHeight = 40;
        return new GlanceScreenRectangle(windowBounds.Left + ((width - targetWidth) / 2),
            windowBounds.Top + ((height - targetHeight) / 2),
            targetWidth,
            targetHeight);
    }

    private void StartAttentionExpansionTimer()
    {
        attentionExpansionTimer ??= CreateAttentionExpansionTimer();
        attentionExpansionTimer.Stop();
        attentionExpansionTimer.Start();
    }

    private DispatcherQueueTimer CreateAttentionExpansionTimer()
    {
        DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(AttentionExpansionDurationMs);
        timer.IsRepeating = false;
        timer.Tick += HandleAttentionExpansionTimerTick;
        return timer;
    }

    private void StopAttentionExpansionTimer() => attentionExpansionTimer?.Stop();

    private void UpdateAssistantDismissalState() => DismissesOnOutsideClick = StaysExpanded &&
            !ViewModel.Assistant.IsOverlayVisible &&
            !ViewModel.Assistant.IsResultPresentationActive;

    private void HandleAttentionExpansionTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();

        if (isPointerOverIsland)
        {
            return;
        }

        if (IsPointerWithinInteractiveRegion)
        {
            isPointerOverIsland = true;
            UpdateComponentInteraction();
            return;
        }

        if (!isContextualDragActive)
        {
            Dismiss();
        }
    }

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
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                if (ViewModel.IsModuleReorderVisible)
                {
                    ShowModuleReorderPresentation();
                    return;
                }

                HideModuleReorderPresentation();
            });
            return;
        }

        if (args.PropertyName == nameof(DesktopIslandViewModel.IsContentRoutePickerVisible))
        {
            if (ViewModel.IsContentRoutePickerVisible)
            {
                CancelConnectedExpansionAnimation();
            }

            _ = DispatcherQueue.TryEnqueue(() =>
            {
                if (ViewModel.IsContentRoutePickerVisible)
                {
                    ShowContentRoutePresentation();
                    return;
                }

                HideContentRoutePresentation();
            });
            return;
        }

        if (args.PropertyName == nameof(DesktopIslandViewModel.SelectedComponent))
        {
            UpdateIslandActivationComponent();
            UpdateExpansionLockComponent();
            UpdateComponentInteraction();
            UpdateFooterAppearanceComponent();

            if (isAssistantPresentationRequested || isContentRoutePresentationRequested || isModuleReorderPresentationRequested)
            {
                BackgroundContent = null;
            }

            return;
        }

        if (args.PropertyName == nameof(DesktopIslandViewModel.IsTransientPresentationActive))
        {
            ApplyExpansionLock();
            bool showTransient = ViewModel.IsTransientPresentationActive;

            if (showTransient)
            {
                BackgroundContent = null;
            }

            Footer.Visibility = Visibility.Collapsed;
            CompactAssistantIndicator.Visibility = Visibility.Collapsed;
            _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low,
                () => TransitionTransientPresentation(showTransient));
            return;
        }

        if (args.PropertyName == nameof(DesktopIslandViewModel.IsTransientExpansionLocked))
        {
            ApplyExpansionLock();
            return;
        }

        if (args.PropertyName == nameof(DesktopIslandViewModel.IsLoadingModules))
        {
            BackgroundContent = ViewModel.IsLoadingModules ||
                isAssistantPresentationRequested ||
                isContentRoutePresentationRequested ||
                isModuleReorderPresentationRequested
                    ? null
                    : GetModuleBackgroundContent();

            if (ViewModel.IsLoadingModules)
            {
                ShowModuleLoadingPresentation();
            }
            else
            {
                int transition = ++moduleLoadingTransition;
                _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                {
                    if (transition == moduleLoadingTransition && !ViewModel.IsLoadingModules)
                    {
                        PlayModuleLoadingCompletionTransition(transition);
                    }
                });
            }

            return;
        }

        if (args.PropertyName == nameof(DesktopIslandViewModel.IsPinned))
        {
            ApplyExpansionLock();
            return;
        }

        if (args.PropertyName == nameof(DesktopIslandViewModel.IsExpanded))
        {
            if (skipNextConnectedExpansion)
            {
                skipNextConnectedExpansion = false;
                return;
            }

            if (HostMode == DesktopIslandHostMode.Taskbar)
            {
                PlayTaskbarConnectedExpansionAnimation();
            }
            else
            {
                PlayConnectedExpansionAnimation();
            }
            return;
        }

        if (args.PropertyName != nameof(DesktopIslandViewModel.SelectedIndex))
        {
            return;
        }

        int selectedIndex = ViewModel.SelectedIndex;
        int direction = selectedIndex > previousIndex ? 1 : -1;
        skipNextConnectedExpansion = true;

        if (previousIndex == ViewModel.ComponentCount - 1 && selectedIndex == 0)
        {
            direction = 1;
        }
        else if (previousIndex == 0 && selectedIndex == ViewModel.ComponentCount - 1)
        {
            direction = -1;
        }

        previousIndex = selectedIndex;

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            skipNextConnectedExpansion = false;
            FluentMotion.PlayHorizontalPageTransition(CompactPresenter, direction);
            FluentMotion.PlayHorizontalPageTransition(ExpandedPresenter, direction);
        });
    }

    private void HandleActualThemeChanged(FrameworkElement sender, object args) => ApplyFooterAppearance();

    private void HandleModuleOrderItemPointerEntered(object sender,
        PointerRoutedEventArgs args)
    {
        if (sender is FrameworkElement element && !ReferenceEquals(element, draggedModuleOrderItem))
        {
            FluentMotion.PlayRouteTargetHover(element);
        }
    }

    private void HandleModuleOrderItemPointerExited(object sender,
        PointerRoutedEventArgs args)
    {
        if (sender is FrameworkElement element && !ReferenceEquals(element, draggedModuleOrderItem))
        {
            FluentMotion.PlayRouteTargetRelease(element);
        }
    }

    private void HandleModuleReorderDragStarting(object sender,
        DragItemsStartingEventArgs args)
    {
        object? item = args.Items.FirstOrDefault();
        draggedModuleOrderItem = item is null
            ? null
            : ModuleReorderList.ContainerFromItem(item) as ListViewItem;

        if (draggedModuleOrderItem is not null)
        {
            Canvas.SetZIndex(draggedModuleOrderItem, 2);
            FluentMotion.PlayRouteTargetHover(draggedModuleOrderItem);
        }
    }

    private void HandleModuleReorderDragCompleted(ListViewBase sender,
        DragItemsCompletedEventArgs args)
    {
        ScrollViewer? scrollViewer = GetModuleReorderScrollViewer();

        moduleOrderDragPreview?.Dispose();
        moduleOrderDragPreview = null;

        if (draggedModuleOrderItem is null)
        {
            return;
        }

        ListViewItem item = draggedModuleOrderItem;
        draggedModuleOrderItem = null;
        Canvas.SetZIndex(item, 0);
        FluentMotion.PlayRouteTargetRelease(item);
        UpdateModuleReorderItemFade(scrollViewer);
    }

    private void HandleModuleReorderDragOver(object sender,
        DragEventArgs args)
    {
        args.DragUIOverride.IsGlyphVisible = false;
        args.DragUIOverride.IsCaptionVisible = false;
        UpdateModuleReorderItemFade(moduleReorderScrollViewer);
    }

    private async void HandleModuleReorderDragStartingVisual(UIElement sender,
        DragStartingEventArgs args)
    {
        ListViewItem? item =
            FindVisualAncestor<ListViewItem>(args.OriginalSource as DependencyObject);

        if (item is null)
        {
            return;
        }

        DragOperationDeferral deferral = args.GetDeferral();

        try
        {
            RenderTargetBitmap renderer = new();
            await renderer.RenderAsync(item);

            if (renderer.PixelWidth <= 0 || renderer.PixelHeight <= 0)
            {
                return;
            }

            IBuffer pixels = await renderer.GetPixelsAsync();
            SoftwareBitmap preview = SoftwareBitmap.CreateCopyFromBuffer(pixels,
                BitmapPixelFormat.Bgra8,
                renderer.PixelWidth,
                renderer.PixelHeight,
                BitmapAlphaMode.Premultiplied);

            moduleOrderDragPreview?.Dispose();
            moduleOrderDragPreview = preview;
            args.DragUI.SetContentFromSoftwareBitmap(preview);
        }
        catch (Exception)
        {
            moduleOrderDragPreview?.Dispose();
            moduleOrderDragPreview = null;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private static T? FindVisualAncestor<T>(DependencyObject? element)
        where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T match)
            {
                return match;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    private static T? FindVisualDescendant<T>(DependencyObject element)
        where T : DependencyObject
    {
        int childCount = VisualTreeHelper.GetChildrenCount(element);

        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(element, index);

            if (child is T match)
            {
                return match;
            }

            T? descendant = FindVisualDescendant<T>(child);

            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private void UpdateFooterAppearanceComponent()
    {
        IGlanceFooterAppearanceComponent? selectedComponent =
            ViewModel.SelectedComponent as IGlanceFooterAppearanceComponent;

        if (ReferenceEquals(footerAppearanceComponent, selectedComponent))
        {
            ApplyFooterAppearance();
            return;
        }

        ClearFooterAppearanceComponent();
        footerAppearanceComponent = selectedComponent;

        footerAppearanceComponent?.FooterAppearanceChanged += HandleFooterAppearanceChanged;

        ApplyFooterAppearance();
    }

    private void ClearFooterAppearanceComponent()
    {
        footerAppearanceComponent?.FooterAppearanceChanged -= HandleFooterAppearanceChanged;
        footerAppearanceComponent = null;
    }

    private void HandleFooterAppearanceChanged(object? sender, EventArgs args) => _ = DispatcherQueue.TryEnqueue(ApplyFooterAppearance);

    private void ApplyFooterAppearance()
    {
        uint value = footerAppearanceComponent?.FooterForegroundColor ??
            (ActualTheme == ElementTheme.Light ? 0xC5000000u : 0xC5FFFFFFu);
        Color color = Color.FromArgb((byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)value);

        SolidColorBrush? foregroundBrush = Resources["GlanceFooterForegroundBrush"] as SolidColorBrush;

        _ = foregroundBrush?.Color = color;

        CompactAssistantIndicator.Foreground = footerAppearanceComponent?.FooterForegroundColor is not null &&
            foregroundBrush is not null ?
            foregroundBrush :
            (Brush)Resources["GlanceDefaultAssistantIndicatorForegroundBrush"];

        if (Resources["GlanceFooterDividerBrush"] is SolidColorBrush dividerBrush)
        {
            dividerBrush.Color = Color.FromArgb(52, color.R, color.G, color.B);
        }
    }

    private void HandleIslandPointerEntered(object sender, PointerRoutedEventArgs args)
    {
        RefreshDisplayLocationIcons();
        StopAttentionExpansionTimer();
        StopInteractionExitTimer();
        isPointerOverIsland = true;
        UpdateComponentInteraction();
        Reveal();
        ViewModel.IsExpanded = true;
    }

    private void RefreshDisplayLocationIcons()
    {
        MonitorHandle monitor = monitorLocator.GetMonitorForWindow(new WindowHandle(Handle));
        bool isTaskbarAtTop = taskbarLocator.GetTaskbarForMonitor(monitor)?.Edge == TaskbarEdge.Top;
        MoveToTaskbarIcon.Glyph = isTaskbarAtTop ? "\uE8AD" : "\uE90E";
        MoveToScreenEdgeIcon.Glyph = isTaskbarAtTop ? "\uEA4F" : "\uE8AB";
        MoveToScreenEdgeIconTransform.Angle = isTaskbarAtTop ? 180 : 0;
    }

    private void HandleIslandPointerExited(object sender, PointerRoutedEventArgs args)
    {
        isPointerOverIsland = false;
        ScheduleInteractionExit();
    }

    private void HandleButtonPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        Button? button = FindButton(args.OriginalSource as DependencyObject);

        if (button is null || !button.IsEnabled)
        {
            return;
        }

        if (!ReferenceEquals(pressedButton, button))
        {
            ReleasePressedButton();
            pressedButton = button;
        }

        FluentMotion.PlayButtonPress(button);
    }

    private void HandleButtonPointerReleased(object sender, PointerRoutedEventArgs args) => ReleasePressedButton();

    private void HandleButtonPointerCanceled(object sender, PointerRoutedEventArgs args) => ReleasePressedButton();

    private void HandleButtonPointerCaptureLost(object sender, PointerRoutedEventArgs args) => ReleasePressedButton();

    private void ReleasePressedButton()
    {
        Button? button = pressedButton;
        pressedButton = null;

        if (button is not null)
        {
            FluentMotion.PlayButtonRelease(button);
        }
    }

    private Button? FindButton(DependencyObject? element)
    {
        while (element is not null && !ReferenceEquals(element, this))
        {
            if (element is Button button)
            {
                return button;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    private void ScheduleInteractionExit()
    {
        interactionExitTimer ??= CreateInteractionExitTimer();
        interactionExitTimer.Stop();
        interactionExitTimer.Start();
    }

    private DispatcherQueueTimer CreateInteractionExitTimer()
    {
        DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(InteractionExitDelayMs);
        timer.IsRepeating = false;
        timer.Tick += HandleInteractionExitTimerTick;
        return timer;
    }

    private void StopInteractionExitTimer() => interactionExitTimer?.Stop();

    private void HandleInteractionExitTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();

        if (isPointerOverIsland)
        {
            return;
        }

        if (IsPointerWithinInteractiveRegion)
        {
            isPointerOverIsland = true;
            UpdateComponentInteraction();
        }
        else
        {
            EndComponentInteraction();
        }
    }

    private void UpdateComponentInteraction()
    {
        IGlanceInteractionAwareComponent? selectedComponent = ViewModel.SelectedComponent as IGlanceInteractionAwareComponent;

        if (ReferenceEquals(interactionComponent, selectedComponent))
        {
            return;
        }

        EndComponentInteraction();

        if (isPointerOverIsland && selectedComponent is not null)
        {
            interactionComponent = selectedComponent;
            interactionComponent.BeginInteraction();
        }
    }

    private void EndComponentInteraction()
    {
        IGlanceInteractionAwareComponent? previousComponent = interactionComponent;
        interactionComponent = null;
        previousComponent?.EndInteraction();
    }

    private void UpdateIslandActivationComponent()
    {
        IGlanceIslandActivationComponent? selectedComponent = ViewModel.SelectedComponent as IGlanceIslandActivationComponent;

        if (ReferenceEquals(islandActivationComponent, selectedComponent))
        {
            ApplyIslandActivationMode();
            return;
        }

        ClearIslandActivationComponent();
        islandActivationComponent = selectedComponent;
        islandActivationComponent?.IslandActivationRequirementChanged += HandleIslandActivationRequirementChanged;
        ApplyIslandActivationMode();
    }

    private void ClearIslandActivationComponent()
    {
        islandActivationComponent?.IslandActivationRequirementChanged -= HandleIslandActivationRequirementChanged;
        islandActivationComponent = null;
        ApplyIslandActivationMode();
    }

    private void HandleIslandActivationRequirementChanged(object? sender,
        EventArgs args) => _ = DispatcherQueue.TryEnqueue(ApplyIslandActivationMode);

    private void UpdateExpansionLockComponent()
    {
        IGlanceExpansionLockComponent? selectedComponent = ViewModel.SelectedComponent as IGlanceExpansionLockComponent;

        if (ReferenceEquals(expansionLockComponent, selectedComponent))
        {
            ApplyExpansionLock();
            return;
        }

        ClearExpansionLockComponent();
        expansionLockComponent = selectedComponent;

        expansionLockComponent?.ExpansionLockChanged += HandleExpansionLockChanged;

        ApplyExpansionLock();
    }

    private void ClearExpansionLockComponent()
    {
        expansionLockComponent?.ExpansionLockChanged -= HandleExpansionLockChanged;
        expansionLockComponent = null;

        ApplyExpansionLock();
    }

    private void HandleExpansionLockChanged(object? sender, EventArgs args) => _ = DispatcherQueue.TryEnqueue(ApplyExpansionLock);

    private void ApplyExpansionLock() => IsExpansionLocked = ViewModel.IsPinned ||
            ViewModel.IsModuleReorderVisible ||
            ViewModel.IsTransientExpansionLocked ||
            expansionLockComponent?.IsExpansionLocked == true;

    private void HandleIslandDeactivated(object? sender, EventArgs args)
    {
        if (expansionLockComponent?.IsExpansionLocked == true)
        {
            expansionLockComponent.DismissExpansionLock();
        }
    }

    private void PlayConnectedExpansionAnimation() => PlayConnectedExpansionAnimation(null);

    private void PlayTaskbarConnectedExpansionAnimation() =>
        PlayConnectedExpansionAnimation(ConfigureTaskbarConnectedExpansionAnimation);

    private void PlayConnectedExpansionAnimation(Action<ConnectedAnimation>? configureAnimation)
    {
        IGlanceComponent? selectedComponent = ViewModel.SelectedComponent;

        if (selectedComponent is not IGlanceConnectedAnimationComponent component)
        {
            return;
        }

        object sourceElement = ViewModel.IsExpanded
            ? component.CompactAnimationElement
            : component.ExpandedAnimationElement;
        object destinationElement = ViewModel.IsExpanded
            ? component.ExpandedAnimationElement
            : component.CompactAnimationElement;

        if (sourceElement is not FrameworkElement source ||
            destinationElement is not FrameworkElement destination ||
            !IsInElementTree(source))
        {
            return;
        }

        ConnectedAnimationService animationService =
            ConnectedAnimationService.GetForCurrentView();
        string animationKey = $"DesktopIsland.{selectedComponent.Id}.Status";

        try
        {
            _ = animationService.PrepareToAnimate(animationKey, source);
        }
        catch (ArgumentException)
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            ConnectedAnimation? animation = animationService.GetAnimation(animationKey);

            if (animation is null || !IsInElementTree(destination))
            {
                return;
            }

            animation.Configuration = new DirectConnectedAnimationConfiguration();
            configureAnimation?.Invoke(animation);

            try
            {
                _ = animation.TryStart(destination);
            }
            catch (ArgumentException)
            {
            }
        });
    }

    private void ConfigureTaskbarConnectedExpansionAnimation(ConnectedAnimation animation)
    {
        Vector3 offset = GetVisualStateTransitionOffset(ViewModel.IsExpanded);

        if (offset.Y == 0)
        {
            return;
        }

        Compositor compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        ScalarKeyFrameAnimation offsetAnimation = compositor.CreateScalarKeyFrameAnimation();
        offsetAnimation.SetScalarParameter("taskbarOffset", offset.Y);
        offsetAnimation.InsertExpressionKeyFrame(0, "StartingValue");
        offsetAnimation.InsertExpressionKeyFrame(1,
            "FinalValue + taskbarOffset",
            CreateVisualStateTransitionEasing());
        offsetAnimation.Duration = GetVisualStateTransitionDuration(ViewModel.IsExpanded);
        animation.SetAnimationComponent(ConnectedAnimationComponent.OffsetY, offsetAnimation);
    }

    private void CancelConnectedExpansionAnimation()
    {
        IGlanceComponent? selectedComponent = ViewModel.SelectedComponent;

        if (selectedComponent is not IGlanceConnectedAnimationComponent)
        {
            return;
        }

        ConnectedAnimation? animation = ConnectedAnimationService.GetForCurrentView()
            .GetAnimation($"DesktopIsland.{selectedComponent.Id}.Status");
        animation?.Cancel();
    }

    private static bool IsInElementTree(FrameworkElement element) => element.IsLoaded && element.XamlRoot is not null;

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

    private async void HandleDragEnter(object sender, DragEventArgs args)
    {
        if (ViewModel.IsModuleReorderVisible)
        {
            return;
        }

        if (!TryGetContentKind(args.DataView, out GlanceContentKind kind))
        {
            args.AcceptedOperation = DataPackageOperation.None;
            ScheduleContextualDragExit();
            return;
        }

        StopContextualDragExitTimer();
        isContextualDragActive = true;
        int session = ++contextualDragSession;
        args.AcceptedOperation = DataPackageOperation.Copy;
        DragOperationDeferral deferral = args.GetDeferral();
        GlanceContentContext? context = null;

        try
        {
            context = await CreateContentContextAsync(args.DataView, kind);
        }
        catch (COMException)
        {
        }
        catch (Exception)
        {
        }
        finally
        {
            await CompleteDropDeferralAsync(deferral);
        }

        if (context is null)
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (isContextualDragActive && session == contextualDragSession)
            {
                _ = ViewModel.TryActivateContent(context);
            }
        });
    }

    private void HandleDragOver(object sender, DragEventArgs args)
    {
        if (ViewModel.IsModuleReorderVisible)
        {
            return;
        }

        if (!TryGetContentKind(args.DataView, out _))
        {
            args.AcceptedOperation = DataPackageOperation.None;
            ScheduleContextualDragExit();
            return;
        }

        StopContextualDragExitTimer();
        args.AcceptedOperation = DataPackageOperation.Copy;
    }

    private void HandleContentRouteDragEnter(object sender, DragEventArgs args)
    {
        if (sender is not FrameworkElement element ||
            element.DataContext is not GlanceContentRoute)
        {
            args.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        StopContextualDragExitTimer();
        SetActiveContentRouteTarget(element);
        args.AcceptedOperation = DataPackageOperation.Copy;
        args.Handled = true;
    }

    private void HandleContentRouteDragOver(object sender, DragEventArgs args)
    {
        StopContextualDragExitTimer();
        args.AcceptedOperation = DataPackageOperation.Copy;
        args.Handled = true;
    }

    private void HandleContentRouteDragLeave(object sender, DragEventArgs args)
    {
        if (ReferenceEquals(sender, activeContentRouteTarget))
        {
            ReleaseActiveContentRouteTarget();
        }

        ScheduleContextualDragExit();
    }

    private void HandleContentRouteDrop(object sender,
        DragEventArgs args)
    {
        if (sender is not FrameworkElement element ||
            element.DataContext is not GlanceContentRoute route)
        {
            args.AcceptedOperation = DataPackageOperation.None;
            args.Handled = true;
            return;
        }

        droppedContentRouteId = route.Id;
        ReleaseActiveContentRouteTarget();
        StopContextualDragExitTimer();
        args.AcceptedOperation = DataPackageOperation.Copy;
    }

    private void SetActiveContentRouteTarget(FrameworkElement target)
    {
        if (ReferenceEquals(activeContentRouteTarget, target))
        {
            return;
        }

        ReleaseActiveContentRouteTarget();
        activeContentRouteTarget = target;
        Canvas.SetZIndex(target, 1);
        FluentMotion.PlayRouteTargetHover(target);
    }

    private void ReleaseActiveContentRouteTarget()
    {
        if (activeContentRouteTarget is null)
        {
            return;
        }

        FrameworkElement target = activeContentRouteTarget;
        activeContentRouteTarget = null;
        Canvas.SetZIndex(target, 0);
        FluentMotion.PlayRouteTargetRelease(target);
    }

    private void HandleDragLeave(object sender, DragEventArgs args)
    {
        if (!ViewModel.IsModuleReorderVisible)
        {
            ScheduleContextualDragExit();
        }
    }

    private bool TryGetContentKind(DataPackageView dataView,
        out GlanceContentKind kind)
    {
        try
        {
            if (dataView.Contains(StandardDataFormats.StorageItems))
            {
                if (ViewModel.CanHandleContent(GlanceContentKind.FilesAndFolders))
                {
                    kind = GlanceContentKind.FilesAndFolders;
                    return true;
                }

                kind = default;
                return false;
            }

            if ((dataView.Contains(StandardDataFormats.WebLink) ||
                dataView.Contains(StandardDataFormats.ApplicationLink)) &&
                ViewModel.CanHandleContent(GlanceContentKind.WebLink))
            {
                kind = GlanceContentKind.WebLink;
                return true;
            }

            if (dataView.Contains(StandardDataFormats.Text) &&
                ViewModel.CanHandleContent(GlanceContentKind.Text))
            {
                kind = GlanceContentKind.Text;
                return true;
            }
        }
        catch (COMException)
        {
        }

        kind = default;
        return false;
    }

    private async void HandleDrop(object sender, DragEventArgs args)
    {
        if (ViewModel.IsModuleReorderVisible)
        {
            return;
        }

        StopContextualDragExitTimer();
        string? routeId = droppedContentRouteId;
        droppedContentRouteId = null;

        if (routeId is not null && !ViewModel.TryActivateContentRoute(routeId))
        {
            CompleteContextualDrag(false);
            return;
        }

        DragOperationDeferral deferral = args.GetDeferral();
        GlanceContentContext? context = null;
        bool contentHandled = false;

        try
        {
            DataPackageView dataView = args.DataView;

            if (TryGetContentKind(dataView, out GlanceContentKind kind))
            {
                context = await CreateContentContextAsync(dataView, kind);
            }
        }
        catch (COMException)
        {
        }
        catch (Exception)
        {
        }
        finally
        {
            await CompleteDropDeferralAsync(deferral);
        }

        if (context is not null)
        {
            try
            {
                contentHandled = await ProcessContentAsync(context);
            }
            catch (Exception)
            {
            }
        }

        CompleteContextualDrag(contentHandled);
    }

    private void ScheduleContextualDragExit()
    {
        if (!isContextualDragActive)
        {
            return;
        }

        contextualDragExitTimer ??= CreateContextualDragExitTimer();
        contextualDragExitTimer.Stop();
        contextualDragExitTimer.Start();
    }

    private DispatcherQueueTimer CreateContextualDragExitTimer()
    {
        DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(ContextualDragExitDelayMs);
        timer.IsRepeating = false;
        timer.Tick += HandleContextualDragExitTimerTick;
        return timer;
    }

    private void StopContextualDragExitTimer() => contextualDragExitTimer?.Stop();

    private void HandleContextualDragExitTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        CompleteContextualDrag(false);
    }

    private void CompleteContextualDrag(bool contentHandled)
    {
        if (!dispatcherQueue.HasThreadAccess)
        {
            _ = dispatcherQueue.TryEnqueue(() => CompleteContextualDrag(contentHandled));
            return;
        }

        StopContextualDragExitTimer();
        isContextualDragActive = false;
        droppedContentRouteId = null;
        contextualDragSession++;

        if (contentHandled)
        {
            ViewModel.CompleteContentRouting(true);
            StaysExpanded = true;
            DismissesOnOutsideClick = true;
            ApplyExpansionLock();
            Reveal();
            return;
        }

        ViewModel.EndContentPreview();
        Dismiss();
    }

    private Task CompleteDropDeferralAsync(DragOperationDeferral deferral)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            CompleteDropDeferral(deferral);
            return Task.CompletedTask;
        }

        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(() =>
        {
            CompleteDropDeferral(deferral);
            _ = completion.TrySetResult(true);
        }))
        {
            _ = completion.TrySetResult(false);
        }

        return completion.Task;
    }

    private static void CompleteDropDeferral(DragOperationDeferral deferral)
    {
        try
        {
            deferral.Complete();
        }
        catch (COMException)
        {
        }
    }

    private Task<bool> ProcessContentAsync(GlanceContentContext context)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            return ViewModel.HandleContentAsync(context);
        }

        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                _ = completion.TrySetResult(await ViewModel.HandleContentAsync(context));
            }
            catch (Exception exception)
            {
                _ = completion.TrySetException(exception);
            }
        }))
        {
            _ = completion.TrySetResult(false);
        }

        return completion.Task;
    }

    private static async Task<GlanceContentContext?> CreateContentContextAsync(DataPackageView dataView,
        GlanceContentKind kind)
    {
        if (kind == GlanceContentKind.FilesAndFolders)
        {
            IReadOnlyList<IStorageItem> storageItems = await dataView.GetStorageItemsAsync();
            GlanceStorageItem[] items = [.. storageItems.Select(CreateStorageItem).OfType<GlanceStorageItem>()];
            return items.Length == 0 ? null : new GlanceContentContext(kind, items);
        }

        if (kind == GlanceContentKind.WebLink)
        {
            Uri? uri = dataView.Contains(StandardDataFormats.WebLink)
                ? await dataView.GetWebLinkAsync()
                : await dataView.GetApplicationLinkAsync();
            return uri is null ? null : new GlanceContentContext(kind, [], uri.AbsoluteUri);
        }

        string text = await dataView.GetTextAsync();
        return string.IsNullOrWhiteSpace(text) ? null : new GlanceContentContext(kind, [], text);
    }

    private static GlanceStorageItem? CreateStorageItem(IStorageItem storageItem)
    {
        try
        {
            string path = storageItem.Path;

            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string name = Path.GetFileName(normalizedPath);

            return new GlanceStorageItem(path, string.IsNullOrWhiteSpace(name) ? storageItem.Name : name, storageItem is StorageFolder);
        }
        catch (COMException)
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect bounds);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
