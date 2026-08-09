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
        TimeSpan duration = TimeSpan.FromSeconds(6.2);
        island.CenterPoint = new Vector3(78, 0, 0);
        handle.CenterPoint = new Vector3(24, 2.5f, 0);

        Vector3KeyFrameAnimation islandScale = compositor.CreateVector3KeyFrameAnimation();
        islandScale.InsertKeyFrame(0, new Vector3(48f / 156f, 5f / 50f, 1));
        islandScale.InsertKeyFrame(0.2f, new Vector3(48f / 156f, 5f / 50f, 1));
        islandScale.InsertKeyFrame(0.3f, Vector3.One, easing);
        islandScale.InsertKeyFrame(0.6f, Vector3.One);
        islandScale.InsertKeyFrame(0.7f, new Vector3(48f / 156f, 5f / 50f, 1), easing);
        islandScale.InsertKeyFrame(1, new Vector3(48f / 156f, 5f / 50f, 1));
        islandScale.Duration = duration;
        islandScale.IterationBehavior = AnimationIterationBehavior.Forever;

        ScalarKeyFrameAnimation islandOpacity = compositor.CreateScalarKeyFrameAnimation();
        islandOpacity.InsertKeyFrame(0, 0);
        islandOpacity.InsertKeyFrame(0.2f, 0);
        islandOpacity.InsertKeyFrame(0.28f, 1, easing);
        islandOpacity.InsertKeyFrame(0.62f, 1);
        islandOpacity.InsertKeyFrame(0.7f, 0, easing);
        islandOpacity.InsertKeyFrame(1, 0);
        islandOpacity.Duration = duration;
        islandOpacity.IterationBehavior = AnimationIterationBehavior.Forever;

        ScalarKeyFrameAnimation handleOpacity = compositor.CreateScalarKeyFrameAnimation();
        handleOpacity.InsertKeyFrame(0, 1);
        handleOpacity.InsertKeyFrame(0.2f, 1);
        handleOpacity.InsertKeyFrame(0.28f, 0, easing);
        handleOpacity.InsertKeyFrame(0.62f, 0);
        handleOpacity.InsertKeyFrame(0.7f, 1, easing);
        handleOpacity.InsertKeyFrame(1, 1);
        handleOpacity.Duration = duration;
        handleOpacity.IterationBehavior = AnimationIterationBehavior.Forever;

        Vector3KeyFrameAnimation handleScale = compositor.CreateVector3KeyFrameAnimation();
        handleScale.InsertKeyFrame(0, Vector3.One);
        handleScale.InsertKeyFrame(0.2f, Vector3.One);
        handleScale.InsertKeyFrame(0.28f, new Vector3(0.72f, 1, 1), easing);
        handleScale.InsertKeyFrame(0.62f, new Vector3(0.72f, 1, 1));
        handleScale.InsertKeyFrame(0.72f, Vector3.One, easing);
        handleScale.InsertKeyFrame(1, Vector3.One);
        handleScale.Duration = duration;
        handleScale.IterationBehavior = AnimationIterationBehavior.Forever;

        Vector3KeyFrameAnimation pointerMovement = compositor.CreateVector3KeyFrameAnimation();
        pointerMovement.InsertKeyFrame(0, new Vector3(30, 28, 0));
        pointerMovement.InsertKeyFrame(0.18f, new Vector3(30, 28, 0));
        pointerMovement.InsertKeyFrame(0.3f, Vector3.Zero, easing);
        pointerMovement.InsertKeyFrame(0.56f, Vector3.Zero);
        pointerMovement.InsertKeyFrame(0.7f, new Vector3(30, 28, 0), easing);
        pointerMovement.InsertKeyFrame(1, new Vector3(30, 28, 0));
        pointerMovement.Duration = duration;
        pointerMovement.IterationBehavior = AnimationIterationBehavior.Forever;
        island.StartAnimation("Scale", islandScale);
        island.StartAnimation("Opacity", islandOpacity);
        handle.StartAnimation("Opacity", handleOpacity);
        handle.StartAnimation("Scale", handleScale);
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
