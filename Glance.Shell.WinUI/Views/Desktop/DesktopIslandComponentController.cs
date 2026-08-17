using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace Glance.Shell.WinUI;

internal sealed class DesktopIslandComponentController :
    IDesktopIslandComponentController
{
    private const int InteractionExitDelayMs = 240;

    private IDesktopIslandComponentHost? host;
    private DispatcherQueueTimer? interactionExitTimer;
    private Button? pressedButton;
    private IGlanceIslandActivationComponent? activationComponent;
    private IGlanceExpansionLockComponent? expansionLockComponent;
    private IGlanceFooterAppearanceComponent? footerAppearanceComponent;
    private IGlanceInteractionAwareComponent? interactionComponent;
    private IGlanceViewAwareComponent? visibleComponent;

    public bool IsPointerOverIsland { get; private set; }

    public void Attach(IDesktopIslandComponentHost host)
    {
        this.host = host;
        SelectedComponentChanged();
    }

    public void Detach()
    {
        ReleasePressedButton();
        ClearActivationComponent();
        ClearExpansionLockComponent();
        ClearFooterAppearanceComponent();
        ClearVisibility();
        EndInteraction();
        StopInteractionExit();
        interactionExitTimer = null;
        IsPointerOverIsland = false;
        host = null;
    }

    public void SelectedComponentChanged()
    {
        UpdateActivationComponent();
        UpdateExpansionLockComponent();
        UpdateInteraction();
        UpdateFooterAppearanceComponent();
        UpdateVisibility();
    }

    public void VisibilityChanged() => UpdateVisibility();

    public void ThemeChanged() => ApplyFooterAppearance();

    public void PointerEntered()
    {
        StopInteractionExit();
        IsPointerOverIsland = true;
        UpdateInteraction();
    }

    public void PointerExited()
    {
        IsPointerOverIsland = false;
        ScheduleInteractionExit();
    }

    public bool RetainInteractionWithinRegion()
    {
        if (!GetHost().IsPointerWithinInteractiveRegion)
        {
            EndInteraction();
            return false;
        }

        IsPointerOverIsland = true;
        UpdateInteraction();
        return true;
    }

    public void StopInteractionExit() => interactionExitTimer?.Stop();

    public void ButtonPressed(PointerRoutedEventArgs args)
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

    public void ButtonReleased() => ReleasePressedButton();

    public void ApplyActivationMode() => GetHost().SetAllowsActivation(activationComponent?.RequiresIslandActivation == true);

    public void ApplyExpansionLock()
    {
        IDesktopIslandComponentHost currentHost = GetHost();
        currentHost.SetExpansionLocked(currentHost.IsPinned || currentHost.IsModuleReorderVisible || currentHost.IsTransientExpansionLocked || expansionLockComponent?.IsExpansionLocked == true);
    }

    public void IslandDeactivated()
    {
        if (expansionLockComponent?.IsExpansionLocked == true)
        {
            expansionLockComponent.DismissExpansionLock();
        }
    }

    private IDesktopIslandComponentHost GetHost() => host ?? throw new InvalidOperationException("The component controller is not attached.");

    private void ScheduleInteractionExit()
    {
        interactionExitTimer ??= CreateInteractionExitTimer();
        interactionExitTimer.Stop();
        interactionExitTimer.Start();
    }

    private DispatcherQueueTimer CreateInteractionExitTimer()
    {
        DispatcherQueueTimer timer = GetHost().DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(InteractionExitDelayMs);
        timer.IsRepeating = false;
        timer.Tick += HandleInteractionExitTimerTick;
        return timer;
    }

    private void HandleInteractionExitTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        _ = RetainInteractionWithinRegion();
    }

    private void UpdateInteraction()
    {
        IGlanceInteractionAwareComponent? selectedComponent =
            GetHost().SelectedComponent as IGlanceInteractionAwareComponent;

        if (ReferenceEquals(interactionComponent, selectedComponent))
        {
            return;
        }

        EndInteraction();

        if (IsPointerOverIsland && selectedComponent is not null)
        {
            interactionComponent = selectedComponent;
            interactionComponent.BeginInteraction();
        }
    }

    private void EndInteraction()
    {
        IGlanceInteractionAwareComponent? previousComponent = interactionComponent;
        interactionComponent = null;
        previousComponent?.EndInteraction();
    }

    private void UpdateVisibility()
    {
        IDesktopIslandComponentHost currentHost = GetHost();
        IGlanceViewAwareComponent? selectedComponent = currentHost.IsSelectedComponentVisible ? currentHost.SelectedComponent as IGlanceViewAwareComponent : null;

        if (ReferenceEquals(visibleComponent, selectedComponent))
        {
            return;
        }

        ClearVisibility();
        visibleComponent = selectedComponent;
        visibleComponent?.EnterView();
    }

    private void ClearVisibility()
    {
        IGlanceViewAwareComponent? previousComponent = visibleComponent;
        visibleComponent = null;
        previousComponent?.LeaveView();
    }

    private void UpdateActivationComponent()
    {
        IGlanceIslandActivationComponent? selectedComponent =
            GetHost().SelectedComponent as IGlanceIslandActivationComponent;

        if (ReferenceEquals(activationComponent, selectedComponent))
        {
            ApplyActivationMode();
            return;
        }

        ClearActivationComponent();
        activationComponent = selectedComponent;
        activationComponent?.IslandActivationRequirementChanged += HandleActivationRequirementChanged;
        ApplyActivationMode();
    }

    private void ClearActivationComponent()
    {
        activationComponent?.IslandActivationRequirementChanged -= HandleActivationRequirementChanged;
        activationComponent = null;

        if (host is not null)
        {
            ApplyActivationMode();
        }
    }

    private void HandleActivationRequirementChanged(object? sender, EventArgs args) => _ = GetHost().DispatcherQueue.TryEnqueue(ApplyActivationMode);

    private void UpdateExpansionLockComponent()
    {
        IGlanceExpansionLockComponent? selectedComponent =
            GetHost().SelectedComponent as IGlanceExpansionLockComponent;

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

        if (host is not null)
        {
            ApplyExpansionLock();
        }
    }

    private void HandleExpansionLockChanged(object? sender, EventArgs args) => _ = GetHost().DispatcherQueue.TryEnqueue(ApplyExpansionLock);

    private void UpdateFooterAppearanceComponent()
    {
        IGlanceFooterAppearanceComponent? selectedComponent =
            GetHost().SelectedComponent as IGlanceFooterAppearanceComponent;

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

    private void HandleFooterAppearanceChanged(object? sender, EventArgs args) => _ = GetHost().DispatcherQueue.TryEnqueue(ApplyFooterAppearance);

    private void ApplyFooterAppearance()
    {
        IDesktopIslandComponentHost currentHost = GetHost();
        uint value = footerAppearanceComponent?.FooterForegroundColor ?? (currentHost.RootElement.ActualTheme == ElementTheme.Light ? 0xC5000000u : 0xC5FFFFFFu);
        Color color = Color.FromArgb((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);
        SolidColorBrush? foregroundBrush =
            currentHost.RootElement.Resources["GlanceFooterForegroundBrush"] as SolidColorBrush;
        _ = foregroundBrush?.Color = color;
        currentHost.CompactAssistantIndicator.Foreground =
            footerAppearanceComponent?.FooterForegroundColor is not null && foregroundBrush is not null ? foregroundBrush : (Brush)currentHost.RootElement.Resources["GlanceDefaultAssistantIndicatorForegroundBrush"];

        if (currentHost.RootElement.Resources["GlanceFooterDividerBrush"] is SolidColorBrush dividerBrush)
        {
            dividerBrush.Color = Color.FromArgb(52, color.R, color.G, color.B);
        }
    }

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
        FrameworkElement rootElement = GetHost().RootElement;

        while (element is not null && !ReferenceEquals(element, rootElement))
        {
            if (element is Button button)
            {
                return button;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }
}



