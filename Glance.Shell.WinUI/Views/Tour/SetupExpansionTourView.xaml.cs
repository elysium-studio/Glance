using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;

namespace Glance.Shell.WinUI;

public sealed partial class SetupExpansionTourView :
    UserControl
{
    public SetupExpansionTourView()
    {
        InitializeComponent();
        Loaded += HandleLoaded;
    }

    public SetupTourViewModel ViewModel => (SetupTourViewModel)DataContext;

    private static CubicBezierEasingFunction CreateEasing(Compositor compositor) =>
        compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1), new Vector2(0.3f, 1));

    private async void HandleCompactClicked(object sender,
        RoutedEventArgs args) => await ViewModel.SelectExpansionModeAsync(GlanceExpansionMode.ExpandOnHover);

    private async void HandleExpandedClicked(object sender,
        RoutedEventArgs args) => await ViewModel.SelectExpansionModeAsync(GlanceExpansionMode.AlwaysExpanded);

    private void HandleLoaded(object sender,
        RoutedEventArgs args)
    {
        AnimateCompactPreview();
        AnimateExpandedPreview();
    }

    private void AnimateCompactPreview()
    {
        Visual island = ElementCompositionPreview.GetElementVisual(CompactIsland);
        Visual pointer = ElementCompositionPreview.GetElementVisual(CompactPointer);
        Compositor compositor = island.Compositor;
        CubicBezierEasingFunction easing = CreateEasing(compositor);
        island.CenterPoint = new Vector3((float)(CompactIsland.Width / 2),
            (float)(CompactIsland.Height / 2),
            0);

        Vector3KeyFrameAnimation scale = compositor.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(0, new Vector3(0.55f, 0.68f, 1));
        scale.InsertKeyFrame(0.28f, new Vector3(0.55f, 0.68f, 1));
        scale.InsertKeyFrame(0.46f, Vector3.One, easing);
        scale.InsertKeyFrame(0.76f, Vector3.One);
        scale.InsertKeyFrame(1, new Vector3(0.55f, 0.68f, 1), easing);
        scale.Duration = TimeSpan.FromSeconds(4.8);
        scale.IterationBehavior = AnimationIterationBehavior.Forever;

        Vector3KeyFrameAnimation movement = compositor.CreateVector3KeyFrameAnimation();
        movement.InsertKeyFrame(0, new Vector3(36, 34, 0));
        movement.InsertKeyFrame(0.3f, Vector3.Zero, easing);
        movement.InsertKeyFrame(0.72f, Vector3.Zero);
        movement.InsertKeyFrame(1, new Vector3(36, 34, 0), easing);
        movement.Duration = scale.Duration;
        movement.IterationBehavior = AnimationIterationBehavior.Forever;
        island.StartAnimation("Scale", scale);
        pointer.StartAnimation("Offset", movement);
    }

    private void AnimateExpandedPreview()
    {
        Visual pointer = ElementCompositionPreview.GetElementVisual(ExpandedPointer);
        Visual activity = ElementCompositionPreview.GetElementVisual(ExpandedActivity);
        Compositor compositor = pointer.Compositor;
        CubicBezierEasingFunction easing = CreateEasing(compositor);
        activity.CenterPoint = new Vector3((float)(ExpandedActivity.ActualWidth / 2), (float)(ExpandedActivity.ActualHeight / 2), 0);

        Vector3KeyFrameAnimation movement = compositor.CreateVector3KeyFrameAnimation();
        movement.InsertKeyFrame(0, new Vector3(34, 28, 0));
        movement.InsertKeyFrame(0.4f, Vector3.Zero, easing);
        movement.InsertKeyFrame(0.68f, Vector3.Zero);
        movement.InsertKeyFrame(1, new Vector3(34, 28, 0), easing);
        movement.Duration = TimeSpan.FromSeconds(4.8);
        movement.IterationBehavior = AnimationIterationBehavior.Forever;

        Vector3KeyFrameAnimation pulse = compositor.CreateVector3KeyFrameAnimation();
        pulse.InsertKeyFrame(0, Vector3.One);
        pulse.InsertKeyFrame(0.5f, new Vector3(1.45f, 1.45f, 1), easing);
        pulse.InsertKeyFrame(1, Vector3.One, easing);
        pulse.Duration = TimeSpan.FromSeconds(1.8);
        pulse.IterationBehavior = AnimationIterationBehavior.Forever;
        pointer.StartAnimation("Offset", movement);
        activity.StartAnimation("Scale", pulse);
    }
}
