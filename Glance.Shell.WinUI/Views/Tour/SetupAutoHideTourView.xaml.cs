using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;

namespace Glance.Shell.WinUI;

public sealed partial class SetupAutoHideTourView :
    UserControl
{
    public SetupAutoHideTourView()
    {
        InitializeComponent();
        Loaded += HandleLoaded;
    }

    public SetupTourViewModel ViewModel => (SetupTourViewModel)DataContext;

    private static CubicBezierEasingFunction CreateEasing(Compositor compositor) =>
        compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1), new Vector2(0.3f, 1));

    private async void HandleAutoHideClicked(object sender,
        RoutedEventArgs args) => await ViewModel.SelectAutoHideAsync(true);

    private async void HandleAlwaysVisibleClicked(object sender,
        RoutedEventArgs args) => await ViewModel.SelectAutoHideAsync(false);

    private void HandleLoaded(object sender,
        RoutedEventArgs args)
    {
        AnimateAutoHide();
        AnimateAlwaysVisible();
    }

    private void AnimateAutoHide()
    {
        Visual island = ElementCompositionPreview.GetElementVisual(AutoHideIsland);
        Visual handle = ElementCompositionPreview.GetElementVisual(AutoHideHandle);
        Visual pointer = ElementCompositionPreview.GetElementVisual(AutoHidePointer);
        Compositor compositor = island.Compositor;
        CubicBezierEasingFunction easing = CreateEasing(compositor);
        Vector3KeyFrameAnimation islandMovement = compositor.CreateVector3KeyFrameAnimation();
        islandMovement.InsertKeyFrame(0, Vector3.Zero);
        islandMovement.InsertKeyFrame(0.24f, Vector3.Zero);
        islandMovement.InsertKeyFrame(0.42f, new Vector3(0, -56, 0), easing);
        islandMovement.InsertKeyFrame(0.62f, new Vector3(0, -56, 0));
        islandMovement.InsertKeyFrame(0.78f, Vector3.Zero, easing);
        islandMovement.InsertKeyFrame(1, Vector3.Zero);
        islandMovement.Duration = TimeSpan.FromSeconds(5.2);
        islandMovement.IterationBehavior = AnimationIterationBehavior.Forever;

        ScalarKeyFrameAnimation islandOpacity = compositor.CreateScalarKeyFrameAnimation();
        islandOpacity.InsertKeyFrame(0, 1);
        islandOpacity.InsertKeyFrame(0.3f, 1);
        islandOpacity.InsertKeyFrame(0.42f, 0);
        islandOpacity.InsertKeyFrame(0.66f, 0);
        islandOpacity.InsertKeyFrame(0.78f, 1, easing);
        islandOpacity.InsertKeyFrame(1, 1);
        islandOpacity.Duration = islandMovement.Duration;
        islandOpacity.IterationBehavior = AnimationIterationBehavior.Forever;

        ScalarKeyFrameAnimation handleOpacity = compositor.CreateScalarKeyFrameAnimation();
        handleOpacity.InsertKeyFrame(0, 0);
        handleOpacity.InsertKeyFrame(0.4f, 0);
        handleOpacity.InsertKeyFrame(0.48f, 0.7f, easing);
        handleOpacity.InsertKeyFrame(0.66f, 0.7f);
        handleOpacity.InsertKeyFrame(0.76f, 0, easing);
        handleOpacity.InsertKeyFrame(1, 0);
        handleOpacity.Duration = islandMovement.Duration;
        handleOpacity.IterationBehavior = AnimationIterationBehavior.Forever;

        Vector3KeyFrameAnimation pointerMovement = compositor.CreateVector3KeyFrameAnimation();
        pointerMovement.InsertKeyFrame(0, new Vector3(30, 28, 0));
        pointerMovement.InsertKeyFrame(0.58f, new Vector3(30, 28, 0));
        pointerMovement.InsertKeyFrame(0.72f, Vector3.Zero, easing);
        pointerMovement.InsertKeyFrame(0.84f, Vector3.Zero);
        pointerMovement.InsertKeyFrame(1, new Vector3(30, 28, 0), easing);
        pointerMovement.Duration = islandMovement.Duration;
        pointerMovement.IterationBehavior = AnimationIterationBehavior.Forever;
        island.StartAnimation("Offset", islandMovement);
        island.StartAnimation("Opacity", islandOpacity);
        handle.StartAnimation("Opacity", handleOpacity);
        pointer.StartAnimation("Offset", pointerMovement);
    }

    private void AnimateAlwaysVisible()
    {
        Visual pointer = ElementCompositionPreview.GetElementVisual(VisiblePointer);
        Visual activity = ElementCompositionPreview.GetElementVisual(VisibleActivity);
        Compositor compositor = pointer.Compositor;
        CubicBezierEasingFunction easing = CreateEasing(compositor);
        activity.CenterPoint = new Vector3((float)(VisibleActivity.ActualWidth / 2), (float)(VisibleActivity.ActualHeight / 2), 0);

        Vector3KeyFrameAnimation pointerMovement = compositor.CreateVector3KeyFrameAnimation();
        pointerMovement.InsertKeyFrame(0, new Vector3(30, 28, 0));
        pointerMovement.InsertKeyFrame(0.45f, Vector3.Zero, easing);
        pointerMovement.InsertKeyFrame(0.68f, Vector3.Zero);
        pointerMovement.InsertKeyFrame(1, new Vector3(30, 28, 0), easing);
        pointerMovement.Duration = TimeSpan.FromSeconds(5.2);
        pointerMovement.IterationBehavior = AnimationIterationBehavior.Forever;

        Vector3KeyFrameAnimation pulse = compositor.CreateVector3KeyFrameAnimation();
        pulse.InsertKeyFrame(0, Vector3.One);
        pulse.InsertKeyFrame(0.5f, new Vector3(1.28f, 1.28f, 1), easing);
        pulse.InsertKeyFrame(1, Vector3.One, easing);
        pulse.Duration = TimeSpan.FromSeconds(2.2);
        pulse.IterationBehavior = AnimationIterationBehavior.Forever;
        pointer.StartAnimation("Offset", pointerMovement);
        activity.StartAnimation("Scale", pulse);
    }
}
