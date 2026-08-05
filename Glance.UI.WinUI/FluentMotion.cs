using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;

namespace Glance.UI.WinUI;

public static class FluentMotion
{
    private static readonly TimeSpan ButtonPressDuration = TimeSpan.FromMilliseconds(90);
    private static readonly TimeSpan ButtonReleaseDuration = TimeSpan.FromMilliseconds(160);
    private static readonly TimeSpan ContentTransitionDuration = TimeSpan.FromMilliseconds(240);
    private static readonly TimeSpan EntranceDuration = TimeSpan.FromMilliseconds(240);
    private static readonly TimeSpan PulseDuration = TimeSpan.FromMilliseconds(320);
    private static readonly TimeSpan RoutePushDuration = TimeSpan.FromMilliseconds(280);
    private static readonly TimeSpan RouteTargetHoverDuration = TimeSpan.FromMilliseconds(140);
    private static readonly TimeSpan RouteTargetReleaseDuration = TimeSpan.FromMilliseconds(180);

    public static void PlayButtonPress(FrameworkElement element) => PlayScale(element, 0.94f, ButtonPressDuration);

    public static void PlayButtonRelease(FrameworkElement element) => PlayScale(element, 1f, ButtonReleaseDuration);

    public static void PlayEntrance(FrameworkElement element, float verticalOffset = 8f)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);

        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = CreateEasing(compositor);

        visual.CenterPoint = new Vector3((float)element.ActualWidth / 2, (float)element.ActualHeight / 2, 0);

        ScalarKeyFrameAnimation opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0, 0f);
        opacity.InsertKeyFrame(1, 1f, easing);
        opacity.Duration = EntranceDuration;

        ScalarKeyFrameAnimation translation = compositor.CreateScalarKeyFrameAnimation();
        translation.InsertKeyFrame(0, verticalOffset);
        translation.InsertKeyFrame(1, 0f, easing);
        translation.Duration = EntranceDuration;

        visual.StartAnimation(nameof(Visual.Opacity), opacity);
        visual.StartAnimation("Translation.Y", translation);
    }

    public static void PlayPulse(FrameworkElement element)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;

        visual.CenterPoint = new Vector3((float)element.ActualWidth / 2, (float)element.ActualHeight / 2, 0);

        Vector3KeyFrameAnimation animation = compositor.CreateVector3KeyFrameAnimation();
        animation.InsertKeyFrame(0, Vector3.One);
        animation.InsertKeyFrame(0.45f, new Vector3(1.14f, 1.14f, 1), CreateEasing(compositor));
        animation.InsertKeyFrame(1, Vector3.One, CreateEasing(compositor));
        animation.Duration = PulseDuration;

        visual.StartAnimation(nameof(Visual.Scale), animation);
    }

    public static void PlayZoomEntrance(FrameworkElement element,
        float initialScale = 0.1f,
        float originX = 0.5f,
        float originY = 0.5f)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = CreateEasing(compositor);
        visual.CenterPoint = new Vector3((float)element.ActualWidth * originX, (float)element.ActualHeight * originY, 0);

        Vector3KeyFrameAnimation scale = compositor.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(0, new Vector3(initialScale, initialScale, 1));
        scale.InsertKeyFrame(1, Vector3.One, easing);
        scale.Duration = TimeSpan.FromMilliseconds(280);

        ScalarKeyFrameAnimation opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0, 0f);
        opacity.InsertKeyFrame(0.35f, 0.85f, easing);
        opacity.InsertKeyFrame(1, 1f, easing);
        opacity.Duration = TimeSpan.FromMilliseconds(220);

        visual.StartAnimation(nameof(Visual.Scale), scale);
        visual.StartAnimation(nameof(Visual.Opacity), opacity);
    }

    public static void PlayHorizontalPageTransition(FrameworkElement element, int direction)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);

        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = CreateEasing(compositor);

        ScalarKeyFrameAnimation translation = compositor.CreateScalarKeyFrameAnimation();
        translation.InsertKeyFrame(0, direction * 36f);
        translation.InsertKeyFrame(1, 0f, easing);
        translation.Duration = TimeSpan.FromMilliseconds(280);

        ScalarKeyFrameAnimation opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0, 0.35f);
        opacity.InsertKeyFrame(1, 1f, easing);
        opacity.Duration = TimeSpan.FromMilliseconds(220);

        visual.StartAnimation("Translation.X", translation);
        visual.StartAnimation(nameof(Visual.Opacity), opacity);
    }

    public static void PlayConnectedContentTransition(FrameworkElement outgoing,
        FrameworkElement incoming,
        FrameworkElement? background,
        bool enteringAssistant,
        Action completed)
    {
        Visual outgoingVisual = ElementCompositionPreview.GetElementVisual(outgoing);
        Visual incomingVisual = ElementCompositionPreview.GetElementVisual(incoming);
        Compositor compositor = outgoingVisual.Compositor;
        CubicBezierEasingFunction entranceEasing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1f));
        CubicBezierEasingFunction exitEasing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.7f, 0f), new Vector2(1f, 0.5f));

        incomingVisual.Opacity = 0;

        CompositionScopedBatch batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

        ScalarKeyFrameAnimation outgoingOpacity = compositor.CreateScalarKeyFrameAnimation();
        outgoingOpacity.InsertKeyFrame(0, 1);
        outgoingOpacity.InsertKeyFrame(1, 0, exitEasing);
        outgoingOpacity.Duration = ContentTransitionDuration;

        ScalarKeyFrameAnimation incomingOpacity = compositor.CreateScalarKeyFrameAnimation();
        incomingOpacity.InsertKeyFrame(0, 0);
        incomingOpacity.InsertKeyFrame(0.22f, 0);
        incomingOpacity.InsertKeyFrame(1, 1, entranceEasing);
        incomingOpacity.Duration = ContentTransitionDuration;

        outgoingVisual.StartAnimation(nameof(Visual.Opacity), outgoingOpacity);
        incomingVisual.StartAnimation(nameof(Visual.Opacity), incomingOpacity);

        if (background is not null)
        {
            Visual backgroundVisual = ElementCompositionPreview.GetElementVisual(background);
            ScalarKeyFrameAnimation backgroundOpacity = compositor.CreateScalarKeyFrameAnimation();
            backgroundOpacity.InsertKeyFrame(0, enteringAssistant ? 1 : 0);
            backgroundOpacity.InsertKeyFrame(1, enteringAssistant ? 0 : 1);
            backgroundOpacity.Duration = ContentTransitionDuration;
            backgroundVisual.StartAnimation(nameof(Visual.Opacity), backgroundOpacity);
        }

        batch.Completed += (_, _) => incoming.DispatcherQueue.TryEnqueue(() => completed());
        batch.End();
    }

    public static void PlayVerticalPushTransition(FrameworkElement outgoing,
        FrameworkElement incoming,
        FrameworkElement? moduleBackground,
        FrameworkElement? outgoingCompanion,
        bool forward,
        Action completed)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(outgoing, true);
        ElementCompositionPreview.SetIsTranslationEnabled(incoming, true);

        Visual outgoingVisual = ElementCompositionPreview.GetElementVisual(outgoing);
        Visual incomingVisual = ElementCompositionPreview.GetElementVisual(incoming);
        Compositor compositor = outgoingVisual.Compositor;
        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1f));
        float distance = (float)Math.Max(outgoing.ActualHeight, incoming.ActualHeight);
        float direction = forward ? -1 : 1;

        outgoingVisual.StopAnimation(nameof(Visual.Opacity));
        incomingVisual.StopAnimation(nameof(Visual.Opacity));
        outgoingVisual.StopAnimation("Translation.Y");
        incomingVisual.StopAnimation("Translation.Y");
        outgoingVisual.Opacity = 1;
        incomingVisual.Opacity = 1;
        outgoingVisual.Properties.InsertVector3("Translation", Vector3.Zero);
        incomingVisual.Properties.InsertVector3("Translation", new Vector3(0, -direction * distance, 0));

        CompositionScopedBatch batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

        ScalarKeyFrameAnimation outgoingTranslation = compositor.CreateScalarKeyFrameAnimation();
        outgoingTranslation.InsertKeyFrame(0, 0);
        outgoingTranslation.InsertKeyFrame(1, direction * distance, easing);
        outgoingTranslation.Duration = RoutePushDuration;

        ScalarKeyFrameAnimation incomingTranslation = compositor.CreateScalarKeyFrameAnimation();
        incomingTranslation.InsertKeyFrame(0, -direction * distance);
        incomingTranslation.InsertKeyFrame(1, 0, easing);
        incomingTranslation.Duration = RoutePushDuration;

        outgoingVisual.StartAnimation("Translation.Y", outgoingTranslation);
        incomingVisual.StartAnimation("Translation.Y", incomingTranslation);

        if (moduleBackground is not null)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(moduleBackground, true);
            Visual backgroundVisual = ElementCompositionPreview.GetElementVisual(moduleBackground);
            backgroundVisual.StopAnimation(nameof(Visual.Opacity));
            backgroundVisual.StopAnimation("Translation.Y");
            backgroundVisual.Opacity = 1;
            backgroundVisual.Properties.InsertVector3("Translation", new Vector3(0, forward ? 0 : -distance, 0));

            ScalarKeyFrameAnimation backgroundTranslation = compositor.CreateScalarKeyFrameAnimation();
            backgroundTranslation.InsertKeyFrame(0, forward ? 0 : -distance);
            backgroundTranslation.InsertKeyFrame(1, forward ? -distance : 0, easing);
            backgroundTranslation.Duration = RoutePushDuration;
            backgroundVisual.StartAnimation("Translation.Y", backgroundTranslation);
        }

        if (outgoingCompanion is not null)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(outgoingCompanion, true);
            Visual companionVisual = ElementCompositionPreview.GetElementVisual(outgoingCompanion);
            companionVisual.StopAnimation("Translation.Y");
            companionVisual.Properties.InsertVector3("Translation", Vector3.Zero);

            ScalarKeyFrameAnimation companionTranslation = compositor.CreateScalarKeyFrameAnimation();
            companionTranslation.InsertKeyFrame(0, 0);
            companionTranslation.InsertKeyFrame(1, direction * distance, easing);
            companionTranslation.Duration = RoutePushDuration;
            companionVisual.StartAnimation("Translation.Y", companionTranslation);
        }

        batch.Completed += (_, _) => incoming.DispatcherQueue.TryEnqueue(() => completed());
        batch.End();
    }

    public static void PlayRouteTargetHover(FrameworkElement element) => PlayRouteTargetTransform(element, 1.055f, -3f, RouteTargetHoverDuration);

    public static void PlayRouteTargetRelease(FrameworkElement element) => PlayRouteTargetTransform(element, 1f, 0, RouteTargetReleaseDuration);

    public static void SetContentPresentationState(FrameworkElement element, bool isVisible)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation(nameof(Visual.Opacity));
        visual.StopAnimation(nameof(Visual.Scale));
        visual.StopAnimation("Translation.X");
        visual.StopAnimation("Translation.Y");
        visual.Opacity = isVisible ? 1 : 0;
        visual.Scale = Vector3.One;
        visual.Properties.InsertVector3("Translation", Vector3.Zero);
        element.IsHitTestVisible = isVisible;
    }

    public static void SetOpacity(FrameworkElement element, float opacity)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation(nameof(Visual.Opacity));
        visual.Opacity = opacity;
    }

    public static void ResetTranslation(FrameworkElement element)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation("Translation.X");
        visual.StopAnimation("Translation.Y");
        visual.Properties.InsertVector3("Translation", Vector3.Zero);
    }

    private static void PlayScale(FrameworkElement element, float scale, TimeSpan duration)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;

        visual.CenterPoint = new Vector3((float)element.ActualWidth / 2, (float)element.ActualHeight / 2, 0);

        Vector3KeyFrameAnimation animation = compositor.CreateVector3KeyFrameAnimation();
        animation.InsertKeyFrame(1, new Vector3(scale, scale, 1), CreateEasing(compositor));
        animation.Duration = duration;

        visual.StartAnimation(nameof(Visual.Scale), animation);
    }

    private static void PlayRouteTargetTransform(FrameworkElement element,
        float scale,
        float translationY,
        TimeSpan duration)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);

        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = CreateEasing(compositor);
        visual.CenterPoint = new Vector3((float)element.ActualWidth / 2, (float)element.ActualHeight / 2, 0);

        Vector3KeyFrameAnimation scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.InsertKeyFrame(1, new Vector3(scale, scale, 1), easing);
        scaleAnimation.Duration = duration;

        ScalarKeyFrameAnimation translationAnimation = compositor.CreateScalarKeyFrameAnimation();
        translationAnimation.InsertKeyFrame(1, translationY, easing);
        translationAnimation.Duration = duration;

        visual.StartAnimation(nameof(Visual.Scale), scaleAnimation);
        visual.StartAnimation("Translation.Y", translationAnimation);
    }

    private static CubicBezierEasingFunction CreateEasing(Compositor compositor) => compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1f), new Vector2(0.3f, 1f));
}
