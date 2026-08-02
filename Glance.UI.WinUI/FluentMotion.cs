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
    private static readonly TimeSpan EntranceDuration = TimeSpan.FromMilliseconds(240);
    private static readonly TimeSpan PulseDuration = TimeSpan.FromMilliseconds(320);

    public static void PlayButtonPress(FrameworkElement element) =>
        PlayScale(element, 0.94f, ButtonPressDuration);

    public static void PlayButtonRelease(FrameworkElement element) =>
        PlayScale(element, 1f, ButtonReleaseDuration);

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

    private static CubicBezierEasingFunction CreateEasing(Compositor compositor) =>
        compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1f), new Vector2(0.3f, 1f));
}
