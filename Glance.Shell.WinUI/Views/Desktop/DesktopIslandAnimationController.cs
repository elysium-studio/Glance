using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Numerics;

namespace Glance.Shell.WinUI;

internal sealed class DesktopIslandAnimationController :
    IDesktopIslandAnimationController
{
    private IDesktopIslandAnimationHost? host;
    private int previousIndex;
    private bool skipNextConnectedExpansion;

    public void Attach(IDesktopIslandAnimationHost host)
    {
        this.host = host;
        previousIndex = host.SelectedIndex;
    }

    public void Detach()
    {
        CancelConnectedAnimation();
        host = null;
        previousIndex = 0;
        skipNextConnectedExpansion = false;
    }

    public void CancelConnectedAnimation()
    {
        IGlanceComponent? selectedComponent = host?.SelectedComponent;

        if (selectedComponent is not IGlanceConnectedAnimationComponent)
        {
            return;
        }

        ConnectedAnimation? animation = ConnectedAnimationService.GetForCurrentView().GetAnimation($"DesktopIsland.{selectedComponent.Id}.Status");
        animation?.Cancel();
    }

    public void ExpandedChanged()
    {
        IDesktopIslandAnimationHost currentHost = GetHost();

        if (skipNextConnectedExpansion)
        {
            skipNextConnectedExpansion = false;
            return;
        }

        PlayConnectedExpansionAnimation(currentHost.HostMode == Elysium.UI.Controls.WinUI.DesktopIslandHostMode.Taskbar ? ConfigureTaskbarConnectedExpansionAnimation : null);
    }

    public void SelectedIndexChanged()
    {
        IDesktopIslandAnimationHost currentHost = GetHost();
        int selectedIndex = currentHost.SelectedIndex;
        int direction = selectedIndex > previousIndex ? 1 : -1;
        skipNextConnectedExpansion = true;

        if (previousIndex == currentHost.ComponentCount - 1 && selectedIndex == 0)
        {
            direction = 1;
        }
        else if (previousIndex == 0 && selectedIndex == currentHost.ComponentCount - 1)
        {
            direction = -1;
        }

        previousIndex = selectedIndex;
        _ = currentHost.DispatcherQueue.TryEnqueue(() =>
        {
            skipNextConnectedExpansion = false;
            FluentMotion.PlayHorizontalPageTransition(currentHost.CompactPresenter, direction);
            FluentMotion.PlayHorizontalPageTransition(currentHost.ExpandedPresenter, direction);
        });
    }

    private IDesktopIslandAnimationHost GetHost() => host ?? throw new InvalidOperationException("The animation controller is not attached.");

    private void PlayConnectedExpansionAnimation(Action<ConnectedAnimation>? configureAnimation)
    {
        IDesktopIslandAnimationHost currentHost = GetHost();
        IGlanceComponent? selectedComponent = currentHost.SelectedComponent;

        if (selectedComponent is not IGlanceConnectedAnimationComponent component)
        {
            return;
        }

        object sourceElement = currentHost.IsExpanded ? component.CompactAnimationElement : component.ExpandedAnimationElement;
        object destinationElement = currentHost.IsExpanded ? component.ExpandedAnimationElement : component.CompactAnimationElement;

        if (sourceElement is not FrameworkElement source || destinationElement is not FrameworkElement destination || !IsInElementTree(source))
        {
            return;
        }

        ConnectedAnimationService animationService = ConnectedAnimationService.GetForCurrentView();
        string animationKey = $"DesktopIsland.{selectedComponent.Id}.Status";

        try
        {
            _ = animationService.PrepareToAnimate(animationKey, source);
        }
        catch (ArgumentException)
        {
            return;
        }

        _ = currentHost.DispatcherQueue.TryEnqueue(() =>
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
        IDesktopIslandAnimationHost currentHost = GetHost();
        Vector3 offset = currentHost.GetTransitionOffset(currentHost.IsExpanded);

        if (offset.Y == 0)
        {
            return;
        }

        Compositor compositor = ElementCompositionPreview.GetElementVisual(currentHost.RootElement).Compositor;
        ScalarKeyFrameAnimation offsetAnimation = compositor.CreateScalarKeyFrameAnimation();
        offsetAnimation.SetScalarParameter("taskbarOffset", offset.Y);
        offsetAnimation.InsertExpressionKeyFrame(0, "StartingValue");
        offsetAnimation.InsertExpressionKeyFrame(1, "FinalValue + taskbarOffset", currentHost.CreateTransitionEasing());
        offsetAnimation.Duration = currentHost.GetTransitionDuration(currentHost.IsExpanded);
        animation.SetAnimationComponent(ConnectedAnimationComponent.OffsetY, offsetAnimation);
    }

    private static bool IsInElementTree(FrameworkElement element) => element.IsLoaded && element.XamlRoot is not null;
}


