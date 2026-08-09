using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;

namespace Glance.Shell.WinUI;

public sealed partial class SetupPlacementTourView :
    UserControl
{
    public SetupPlacementTourView()
    {
        InitializeComponent();
        Loaded += HandleLoaded;
    }

    public SetupTourViewModel ViewModel => (SetupTourViewModel)DataContext;

    private static CubicBezierEasingFunction CreateEasing(Compositor compositor) =>
        compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1), new Vector2(0.3f, 1));

    private async void HandleTopClicked(object sender,
        RoutedEventArgs args) => await ViewModel.SelectPlacementAsync(GlancePlacement.Top);

    private async void HandleBottomClicked(object sender,
        RoutedEventArgs args) => await ViewModel.SelectPlacementAsync(GlancePlacement.Bottom);

    private void HandleLoaded(object sender,
        RoutedEventArgs args)
    {
        AnimatePreview(TopIsland, 8);
        AnimatePreview(BottomIsland, -8);
    }

    private static void AnimatePreview(FrameworkElement element,
        float verticalOffset)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = CreateEasing(compositor);
        Vector3KeyFrameAnimation movement = compositor.CreateVector3KeyFrameAnimation();
        movement.InsertKeyFrame(0, new Vector3(0, verticalOffset, 0));
        movement.InsertKeyFrame(0.34f, Vector3.Zero, easing);
        movement.InsertKeyFrame(0.72f, Vector3.Zero);
        movement.InsertKeyFrame(1, new Vector3(0, verticalOffset, 0), easing);
        movement.Duration = TimeSpan.FromSeconds(4.8);
        movement.IterationBehavior = AnimationIterationBehavior.Forever;
        visual.StartAnimation("Offset", movement);
    }
}
