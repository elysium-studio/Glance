using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using System;
using System.Numerics;

namespace Glance.Shell.WinUI;

public sealed partial class SetupModulesTourView :
    UserControl
{
    public SetupModulesTourView()
    {
        InitializeComponent();
        Loaded += HandleLoaded;
    }

    public SetupTourViewModel ViewModel => (SetupTourViewModel)DataContext;

    private static CubicBezierEasingFunction CreateEasing(Compositor compositor) =>
        compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1), new Vector2(0.3f, 1));

    private void HandleLoaded(object sender,
        RoutedEventArgs args)
    {
        AnimateAmbientGlow(AmbientGlowOne, 0);
        AnimateAmbientGlow(AmbientGlowTwo, 2100);
        AnimateSelectedCard();
    }

    private void HandleSelectionChanged(object sender,
        SelectionChangedEventArgs args) => _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, AnimateSelectedCard);

    private void AnimateAmbientGlow(UIElement element, float delay)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;
        visual.CenterPoint = new Vector3((float)(element.RenderSize.Width / 2), (float)(element.RenderSize.Height / 2), 0);
        Vector3KeyFrameAnimation scale = compositor.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(0, Vector3.One);
        scale.InsertKeyFrame(0.5f, new Vector3(1.14f, 1.14f, 1), CreateEasing(compositor));
        scale.InsertKeyFrame(1, Vector3.One, CreateEasing(compositor));
        scale.Duration = TimeSpan.FromSeconds(7.5);
        scale.DelayTime = TimeSpan.FromMilliseconds(delay);
        scale.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        scale.IterationBehavior = AnimationIterationBehavior.Forever;
        visual.StartAnimation("Scale", scale);
    }

    private void AnimateSelectedCard()
    {
        if (ModuleCarousel.ContainerFromIndex(ModuleCarousel.SelectedIndex) is not FlipViewItem container)
        {
            return;
        }

        ElementCompositionPreview.SetIsTranslationEnabled(container, true);
        Visual visual = ElementCompositionPreview.GetElementVisual(container);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = CreateEasing(compositor);
        visual.CenterPoint = new Vector3((float)(container.ActualWidth / 2), (float)(container.ActualHeight / 2), 0);
        visual.Properties.InsertVector3("Translation", new Vector3(34, 0, 0));
        visual.Opacity = 0;
        visual.Scale = new Vector3(0.94f, 0.94f, 1);

        Vector3KeyFrameAnimation translation = compositor.CreateVector3KeyFrameAnimation();
        translation.InsertKeyFrame(0, new Vector3(34, 0, 0));
        translation.InsertKeyFrame(1, Vector3.Zero, easing);
        translation.Duration = TimeSpan.FromMilliseconds(460);
        ScalarKeyFrameAnimation opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0, 0);
        opacity.InsertKeyFrame(1, 1, easing);
        opacity.Duration = translation.Duration;
        Vector3KeyFrameAnimation scale = compositor.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(0, new Vector3(0.94f, 0.94f, 1));
        scale.InsertKeyFrame(1, Vector3.One, easing);
        scale.Duration = translation.Duration;
        visual.StartAnimation("Translation", translation);
        visual.StartAnimation("Opacity", opacity);
        visual.StartAnimation("Scale", scale);
    }

    private void HandleCardPointerEntered(object sender,
        PointerRoutedEventArgs args)
    {
        if (sender is UIElement element)
        {
            AnimateCardScale(element, 1.018f);
        }
    }

    private void HandleCardPointerExited(object sender,
        PointerRoutedEventArgs args)
    {
        if (sender is UIElement element)
        {
            AnimateCardScale(element, 1);
        }
    }

    private static void AnimateCardScale(UIElement element, float target)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;
        visual.CenterPoint = new Vector3((float)(element.RenderSize.Width / 2), (float)(element.RenderSize.Height / 2), 0);
        Vector3KeyFrameAnimation animation = compositor.CreateVector3KeyFrameAnimation();
        animation.InsertKeyFrame(1, new Vector3(target, target, 1), CreateEasing(compositor));
        animation.Duration = TimeSpan.FromMilliseconds(220);
        visual.StartAnimation("Scale", animation);
    }
}
