using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.ComponentModel;
using System.Numerics;

namespace Glance.Shell.WinUI;

internal sealed class DesktopIslandPresentationController :
    IDesktopIslandPresentationController
{
    private const string AssistantContinuumAnimationKey = "DesktopIsland.Assistant.Continuum";
    private const int AttentionExpansionDurationMs = 4000;
    private const int ModuleLoadingCrossFadeDurationMs = 360;
    private const int StartupAttentionDelayMs = 2500;

    private readonly IDesktopIslandComponentController componentController;
    private readonly IDesktopIslandDropController dropController;
    private readonly IDesktopIslandModuleReorderController moduleReorderController;
    private IDesktopIslandPresentationHost? host;
    private DispatcherQueueTimer? attentionExpansionTimer;
    private DispatcherQueueTimer? startupAttentionTimer;
    private int assistantPresentationTransition;
    private int contentRoutePresentationTransition;
    private int moduleLoadingTransition;
    private int moduleReorderPresentationTransition;
    private int transientPresentationTransition;

    public DesktopIslandPresentationController(IDesktopIslandComponentController componentController, IDesktopIslandDropController dropController, IDesktopIslandModuleReorderController moduleReorderController)
    {
        this.componentController = componentController;
        this.dropController = dropController;
        this.moduleReorderController = moduleReorderController;
    }

    public bool IsAssistantRequested { get; private set; }

    public bool IsContentRouteRequested { get; private set; }

    public bool IsModuleReorderRequested { get; private set; }

    public void Attach(IDesktopIslandPresentationHost host)
    {
        this.host = host;
        ViewModel.AttentionReceived += HandleAttentionReceived;
        ViewModel.Assistant.PropertyChanged += HandleAssistantPropertyChanged;
        ViewModel.Assistant.WakeWordDetected += HandleWakeWordDetected;
    }

    public void Detach()
    {
        if (host is not null)
        {
            ViewModel.AttentionReceived -= HandleAttentionReceived;
            ViewModel.Assistant.PropertyChanged -= HandleAssistantPropertyChanged;
            ViewModel.Assistant.WakeWordDetected -= HandleWakeWordDetected;
        }

        StopAttentionExpansion();
        StopStartupAttentionTimer();
        attentionExpansionTimer = null;
        startupAttentionTimer = null;
        IsAssistantRequested = false;
        IsContentRouteRequested = false;
        IsModuleReorderRequested = false;
        assistantPresentationTransition++;
        contentRoutePresentationTransition++;
        moduleLoadingTransition++;
        moduleReorderPresentationTransition++;
        transientPresentationTransition++;
        host = null;
    }

    public void Initialize()
    {
        ApplyAssistantPresentation(ViewModel.Assistant.IsOverlayVisible);
        ApplyContentRoutePresentation(ViewModel.IsContentRoutePickerVisible);
        ApplyModuleReorderPresentation(ViewModel.IsModuleReorderVisible);
        InitializeModuleLoadingPresentation();
        componentController.VisibilityChanged();
        StartStartupAttentionTimer();
    }

    public void StopAttentionExpansion() => attentionExpansionTimer?.Stop();

    public void TransientPresentationChanged()
    {
        componentController.VisibilityChanged();
        componentController.ApplyExpansionLock();
        bool showTransient = ViewModel.IsTransientPresentationActive;

        if (showTransient)
        {
            Host.BackgroundContent = null;
        }

        Host.Footer.Visibility = Visibility.Collapsed;
        Host.CompactAssistantIndicator.Visibility = Visibility.Collapsed;
        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => TransitionTransientPresentation(showTransient));
    }

    public void LoadingModulesChanged()
    {
        componentController.VisibilityChanged();
        Host.BackgroundContent = ViewModel.IsLoadingModules || IsAssistantRequested || IsContentRouteRequested || IsModuleReorderRequested ? null : Host.GetModuleBackgroundContent();

        if (ViewModel.IsLoadingModules)
        {
            ShowModuleLoadingPresentation();
            return;
        }

        int transition = ++moduleLoadingTransition;
        _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (transition == moduleLoadingTransition && !ViewModel.IsLoadingModules)
            {
                PlayModuleLoadingCompletionTransition(transition);
            }
        });
    }

    public void ContentRouteVisibilityChanged()
    {
        componentController.VisibilityChanged();
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (ViewModel.IsContentRoutePickerVisible)
            {
                ShowContentRoutePresentation();
                return;
            }

            HideContentRoutePresentation();
        });
    }

    public void ModuleReorderVisibilityChanged()
    {
        componentController.VisibilityChanged();
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (ViewModel.IsModuleReorderVisible)
            {
                ShowModuleReorderPresentation();
                return;
            }

            HideModuleReorderPresentation();
        });
    }

    private IDesktopIslandPresentationHost Host => host ?? throw new InvalidOperationException("The presentation controller is not attached.");

    private DispatcherQueue DispatcherQueue => Host.DispatcherQueue;

    private DesktopIslandViewModel ViewModel => Host.ViewModel;

    private void InitializeModuleLoadingPresentation()
    {
        if (!Host.IsLoaded)
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
        Host.CompactModuleLoadingView.Visibility = Visibility.Visible;
        Host.ExpandedModuleLoadingView.Visibility = Visibility.Visible;
        FluentMotion.SetOpacity(Host.CompactModuleLoadingView, 1);
        FluentMotion.SetOpacity(Host.ExpandedModuleLoadingView, 1);
    }

    private void HideModuleLoadingPresentation()
    {
        Host.CompactModuleLoadingView.Visibility = Visibility.Collapsed;
        Host.ExpandedModuleLoadingView.Visibility = Visibility.Collapsed;
        FluentMotion.SetOpacity(Host.CompactModuleLoadingView, 1);
        FluentMotion.SetOpacity(Host.ExpandedModuleLoadingView, 1);
        FluentMotion.SetOpacity(Host.CompactPresenter, 1);
        FluentMotion.SetOpacity(Host.ExpandedPresenter, 1);
        FluentMotion.SetOpacity(Host.TransientCompactPresenter, 1);
        FluentMotion.SetOpacity(Host.TransientExpandedPresenter, 1);
        FluentMotion.SetOpacity(Host.CompactAssistantIndicator, 1);
        FluentMotion.SetOpacity(Host.Footer, 1);
        ApplyTransientPresentation(ViewModel.IsTransientPresentationActive);

        if (Host.BackgroundElement is FrameworkElement background)
        {
            FluentMotion.SetOpacity(background, 1);
        }
    }

    private void TransitionTransientPresentation(bool showTransient, bool allowLayoutRetry = true)
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

        FrameworkElement outgoing = ViewModel.IsExpanded ? showTransient ? Host.ExpandedPresenter : Host.TransientExpandedPresenter : showTransient ? Host.CompactPresenter : Host.TransientCompactPresenter;
        FrameworkElement incoming = ViewModel.IsExpanded ? showTransient ? Host.TransientExpandedPresenter : Host.ExpandedPresenter : showTransient ? Host.TransientCompactPresenter : Host.CompactPresenter;
        outgoing.Visibility = Visibility.Visible;
        incoming.Visibility = Visibility.Visible;
        Host.Footer.Visibility = Visibility.Collapsed;
        Host.CompactAssistantIndicator.Visibility = Visibility.Collapsed;
        Host.UpdateLayout();

        if (!IsInElementTree(outgoing) || !IsInElementTree(incoming) || outgoing.ActualWidth <= 0 || outgoing.ActualHeight <= 0 || incoming.ActualWidth <= 0 || incoming.ActualHeight <= 0)
        {
            if (allowLayoutRetry)
            {
                _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                {
                    if (transition == transientPresentationTransition && showTransient == ViewModel.IsTransientPresentationActive)
                    {
                        TransitionTransientPresentation(showTransient, false);
                    }
                });
                return;
            }

            CompleteTransientPresentationTransition(transition, showTransient);
            return;
        }

        FluentMotion.PlayTransientPushTransition(outgoing, incoming, showTransient, () => CompleteTransientPresentationTransition(transition, showTransient));
    }

    private void CompleteTransientPresentationTransition(int transition, bool showTransient)
    {
        if (transition != transientPresentationTransition || showTransient != ViewModel.IsTransientPresentationActive)
        {
            return;
        }

        ApplyTransientPresentation(showTransient);

        if (!showTransient)
        {
            ViewModel.CompleteTransientPresentationDismissal();
            Host.BackgroundContent = Host.GetModuleBackgroundContent();
        }
    }

    private void ApplyTransientPresentation(bool showTransient)
    {
        SetPresenterState(Host.CompactPresenter, !showTransient);
        SetPresenterState(Host.ExpandedPresenter, !showTransient);
        SetPresenterState(Host.TransientCompactPresenter, showTransient);
        SetPresenterState(Host.TransientExpandedPresenter, showTransient);
        Host.Footer.Visibility = Host.BindingPolicy.WhenPrimaryContentVisible(ViewModel.IsLoadingModules, showTransient);
        Host.CompactAssistantIndicator.Visibility = Host.BindingPolicy.WhenAssistantAvailable(ViewModel.Assistant.IsAvailable, ViewModel.Assistant.IsEnabled, ViewModel.IsLoadingModules, showTransient);
    }

    private static void SetPresenterState(FrameworkElement presenter, bool isVisible)
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
        if (!Host.IsLoaded || transition != moduleLoadingTransition)
        {
            return;
        }

        Host.CompactModuleLoadingView.Visibility = Visibility.Visible;
        Host.ExpandedModuleLoadingView.Visibility = Visibility.Visible;
        bool isTransientPresentationActive = ViewModel.IsTransientPresentationActive;
        FrameworkElement compactIncoming = isTransientPresentationActive ? Host.TransientCompactPresenter : Host.CompactPresenter;
        FrameworkElement expandedIncoming = isTransientPresentationActive ? Host.TransientExpandedPresenter : Host.ExpandedPresenter;
        compactIncoming.Visibility = Visibility.Visible;
        expandedIncoming.Visibility = Visibility.Visible;

        if (!isTransientPresentationActive)
        {
            Host.Footer.Visibility = Visibility.Visible;
        }

        Host.UpdateLayout();
        FrameworkElement? background = Host.BackgroundElement;
        Visual compactLoadingVisual = ElementCompositionPreview.GetElementVisual(Host.CompactModuleLoadingView);
        Visual expandedLoadingVisual = ElementCompositionPreview.GetElementVisual(Host.ExpandedModuleLoadingView);
        Visual compactPresenterVisual = ElementCompositionPreview.GetElementVisual(compactIncoming);
        Visual expandedPresenterVisual = ElementCompositionPreview.GetElementVisual(expandedIncoming);
        Visual assistantIndicatorVisual = ElementCompositionPreview.GetElementVisual(Host.CompactAssistantIndicator);
        Visual footerVisual = ElementCompositionPreview.GetElementVisual(Host.Footer);
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

    private static void StartOpacityAnimation(Compositor compositor, Visual visual, float from, float to, TimeSpan duration, CompositionEasingFunction easing)
    {
        ScalarKeyFrameAnimation animation = compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, easing);
        animation.Duration = duration;
        visual.StartAnimation(nameof(Visual.Opacity), animation);
    }

    private static void SetOpacity(Visual visual, float opacity)
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
        Host.Reveal();

        if (request.Expand)
        {
            StartAttentionExpansionTimer();
        }

        FrameworkElement presenter = ViewModel.IsExpanded ? Host.ExpandedPresenter : Host.CompactPresenter;
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
        if (IsAssistantRequested || !ViewModel.Assistant.IsOverlayVisible)
        {
            return;
        }

        IsAssistantRequested = true;
        PrepareAssistantContinuumAnimation(true);
        Host.StaysExpanded = true;
        UpdateAssistantDismissalState();
        componentController.ApplyExpansionLock();
        Host.Reveal();
        ViewModel.IsExpanded = true;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (IsAssistantRequested)
            {
                TransitionAssistantPresentation(true);
            }
        });
    }

    private void HideAssistantPresentation()
    {
        if (!IsAssistantRequested)
        {
            return;
        }

        PrepareAssistantContinuumAnimation(false);
        IsAssistantRequested = false;
        TransitionAssistantPresentation(false);
    }

    private void TransitionAssistantPresentation(bool showAssistant, bool allowLayoutRetry = true)
    {
        if (showAssistant != IsAssistantRequested)
        {
            return;
        }

        int transition = ++assistantPresentationTransition;
        FrameworkElement outgoing = showAssistant ? Host.ExpandedModuleSurface : Host.AssistantOverlayPresenter;
        FrameworkElement incoming = showAssistant ? Host.AssistantOverlayPresenter : Host.ExpandedModuleSurface;
        outgoing.Visibility = Visibility.Visible;
        incoming.Visibility = Visibility.Visible;
        Host.UpdateLayout();

        if (!showAssistant)
        {
            Host.BackgroundContent = Host.GetModuleBackgroundContent();
        }

        if (!IsInElementTree(outgoing) || !IsInElementTree(incoming) || outgoing.ActualWidth <= 0 || outgoing.ActualHeight <= 0 || incoming.ActualWidth <= 0 || incoming.ActualHeight <= 0)
        {
            if (allowLayoutRetry)
            {
                _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                {
                    if (showAssistant == IsAssistantRequested)
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
        FrameworkElement? background = Host.BackgroundElement;
        FluentMotion.PlayConnectedContentTransition(outgoing, incoming, background, showAssistant, () =>
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
        IsAssistantRequested = showAssistant;
        componentController.VisibilityChanged();

        if (showAssistant)
        {
            Host.StaysExpanded = true;
            UpdateAssistantDismissalState();
            componentController.ApplyExpansionLock();
        }

        FluentMotion.SetContentPresentationState(Host.ExpandedModuleSurface, !showAssistant);
        FluentMotion.SetContentPresentationState(Host.AssistantOverlayPresenter, showAssistant);
        Host.ExpandedModuleSurface.Visibility = showAssistant ? Visibility.Collapsed : Visibility.Visible;
        Host.AssistantOverlayPresenter.Visibility = showAssistant ? Visibility.Visible : Visibility.Collapsed;
        Host.BackgroundContent = showAssistant ? null : Host.GetModuleBackgroundContent();
    }

    private void CompleteAssistantPresentationExit()
    {
        ViewModel.IsExpanded = true;
        componentController.ApplyExpansionLock();
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
            animation.Configuration = showAssistant ? new GravityConnectedAnimationConfiguration() : new DirectConnectedAnimationConfiguration();
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
        ContentControl indicator = ViewModel.IsExpanded ? Host.ExpandedAssistantIndicator : Host.CompactAssistantIndicator;
        return (indicator.Content as IGlanceAssistantConnectedAnimationView)?.ConnectedAnimationElement as FrameworkElement;
    }

    private FrameworkElement? GetAssistantOverlayAnimationElement() => (Host.AssistantOverlayPresenter.Content as IGlanceAssistantConnectedAnimationView)?.ConnectedAnimationElement as FrameworkElement;

    private void ShowContentRoutePresentation()
    {
        if (IsContentRouteRequested || !ViewModel.IsContentRoutePickerVisible)
        {
            return;
        }

        IsContentRouteRequested = true;
        Host.Reveal();
        ViewModel.IsExpanded = true;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (IsContentRouteRequested)
            {
                TransitionContentRoutePresentation(true);
            }
        });
    }

    private void HideContentRoutePresentation()
    {
        if (!IsContentRouteRequested)
        {
            return;
        }

        IsContentRouteRequested = false;
        TransitionContentRoutePresentation(false);
    }

    private void TransitionContentRoutePresentation(bool showRoutes, bool allowLayoutRetry = true)
    {
        if (showRoutes != IsContentRouteRequested)
        {
            return;
        }

        int transition = ++contentRoutePresentationTransition;
        FrameworkElement outgoing = showRoutes ? Host.ExpandedModuleSurface : Host.ContentRoutePicker;
        FrameworkElement incoming = showRoutes ? Host.ContentRoutePicker : Host.ExpandedModuleSurface;
        outgoing.Visibility = Visibility.Visible;
        incoming.Visibility = Visibility.Visible;
        Host.UpdateLayout();

        if (!showRoutes)
        {
            Host.BackgroundContent = Host.GetModuleBackgroundContent();
        }

        if (!IsInElementTree(outgoing) || !IsInElementTree(incoming) || outgoing.ActualWidth <= 0 || outgoing.ActualHeight <= 0 || incoming.ActualWidth <= 0 || incoming.ActualHeight <= 0)
        {
            if (allowLayoutRetry)
            {
                _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                {
                    if (showRoutes == IsContentRouteRequested)
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
        Host.ExpandedContentHost.Clip = new RectangleGeometry
        {
            Rect = new Windows.Foundation.Rect(0, 0, Host.ExpandedContentHost.ActualWidth, Host.ExpandedContentHost.ActualHeight)
        };
        FrameworkElement? background = Host.BackgroundElement;
        FrameworkElement? compactContent = showRoutes ? Host.CompactTemplateContent : null;
        FluentMotion.PlayVerticalPushTransition(outgoing, incoming, background, compactContent, showRoutes, () =>
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
        Host.ExpandedContentHost.Clip = null;

        if (!showRoutes)
        {
            dropController.ReleaseActiveRouteTarget();
        }

        IsContentRouteRequested = showRoutes;
        componentController.VisibilityChanged();
        FluentMotion.SetContentPresentationState(Host.ExpandedModuleSurface, !showRoutes);
        FluentMotion.SetContentPresentationState(Host.ContentRoutePicker, showRoutes);
        Host.ExpandedModuleSurface.Visibility = showRoutes ? Visibility.Collapsed : Visibility.Visible;
        Host.ContentRoutePicker.Visibility = showRoutes ? Visibility.Visible : Visibility.Collapsed;
        Host.BackgroundContent = showRoutes ? null : Host.GetModuleBackgroundContent();
    }

    private void ShowModuleReorderPresentation()
    {
        if (IsModuleReorderRequested || !ViewModel.IsModuleReorderVisible)
        {
            return;
        }

        IsModuleReorderRequested = true;
        Host.StaysExpanded = true;
        Host.DismissesOnOutsideClick = false;
        componentController.ApplyExpansionLock();
        Host.Reveal();
        ViewModel.IsExpanded = true;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (IsModuleReorderRequested)
            {
                TransitionModuleReorderPresentation(true);
            }
        });
    }

    private void HideModuleReorderPresentation()
    {
        if (!IsModuleReorderRequested)
        {
            return;
        }

        IsModuleReorderRequested = false;
        TransitionModuleReorderPresentation(false);
    }

    private void TransitionModuleReorderPresentation(bool showReorder, bool allowLayoutRetry = true)
    {
        if (showReorder != IsModuleReorderRequested)
        {
            return;
        }

        int transition = ++moduleReorderPresentationTransition;
        FrameworkElement outgoing = showReorder ? Host.ExpandedModuleSurface : Host.ModuleReorderSurface;
        FrameworkElement incoming = showReorder ? Host.ModuleReorderSurface : Host.ExpandedModuleSurface;
        outgoing.Visibility = Visibility.Visible;
        incoming.Visibility = Visibility.Visible;

        if (!showReorder)
        {
            Host.BackgroundContent = Host.GetModuleBackgroundContent();
        }

        Host.UpdateLayout();

        if (showReorder && ViewModel.SelectedComponent is not null)
        {
            moduleReorderController.CenterSelected();
        }

        if (!IsInElementTree(outgoing) || !IsInElementTree(incoming) || outgoing.ActualWidth <= 0 || outgoing.ActualHeight <= 0 || incoming.ActualWidth <= 0 || incoming.ActualHeight <= 0)
        {
            if (allowLayoutRetry)
            {
                _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                {
                    if (showReorder == IsModuleReorderRequested)
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
        FluentMotion.PlaySemanticZoomTransition(outgoing, incoming, Host.BackgroundElement, showReorder, () =>
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

    private void ApplyModuleReorderPresentation(bool showReorder)
    {
        IsModuleReorderRequested = showReorder;
        componentController.VisibilityChanged();

        if (showReorder)
        {
            Host.StaysExpanded = true;
            Host.DismissesOnOutsideClick = false;
            componentController.ApplyExpansionLock();
        }

        FluentMotion.SetContentPresentationState(Host.ExpandedModuleSurface, !showReorder);
        FluentMotion.SetContentPresentationState(Host.ModuleReorderSurface, showReorder);
        Host.ExpandedModuleSurface.Visibility = showReorder ? Visibility.Collapsed : Visibility.Visible;
        Host.ModuleReorderSurface.Visibility = showReorder ? Visibility.Visible : Visibility.Collapsed;
        Host.BackgroundContent = showReorder ? null : Host.GetModuleBackgroundContent();
    }

    private void CompleteModuleReorderPresentationExit()
    {
        Host.StaysExpanded = false;
        Host.DismissesOnOutsideClick = false;
        ViewModel.IsExpanded = true;
        componentController.ApplyExpansionLock();
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

    private void UpdateAssistantDismissalState() => Host.DismissesOnOutsideClick =
        Host.StaysExpanded && !ViewModel.Assistant.IsOverlayVisible && !ViewModel.Assistant.IsResultPresentationActive;

    private void HandleAttentionExpansionTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();

        if (componentController.IsPointerOverIsland || componentController.RetainInteractionWithinRegion())
        {
            return;
        }

        if (!dropController.IsActive)
        {
            Host.Dismiss();
        }
    }

    private static bool IsInElementTree(FrameworkElement element) => element.IsLoaded && element.XamlRoot is not null;
}
