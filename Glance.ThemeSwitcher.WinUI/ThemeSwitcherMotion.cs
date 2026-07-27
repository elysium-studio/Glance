using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;

namespace Glance.ThemeSwitcher.WinUI;

internal static class ThemeSwitcherMotion
{
    public static void Play(FrameworkElement element)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;
        visual.CenterPoint = new Vector3((float)element.ActualWidth / 2, (float)element.ActualHeight / 2, 0);

        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1), new Vector2(0.3f, 1));
        Vector3KeyFrameAnimation scale = compositor.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(0, new Vector3(0.72f), easing);
        scale.InsertKeyFrame(0.55f, new Vector3(1.12f), easing);
        scale.InsertKeyFrame(1, Vector3.One, easing);
        scale.Duration = TimeSpan.FromMilliseconds(320);

        ScalarKeyFrameAnimation rotation = compositor.CreateScalarKeyFrameAnimation();
        rotation.InsertKeyFrame(0, -28, easing);
        rotation.InsertKeyFrame(1, 0, easing);
        rotation.Duration = TimeSpan.FromMilliseconds(320);

        ScalarKeyFrameAnimation opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0, 0.35f);
        opacity.InsertKeyFrame(1, 1);
        opacity.Duration = TimeSpan.FromMilliseconds(220);

        visual.StartAnimation(nameof(Visual.Scale), scale);
        visual.StartAnimation(nameof(Visual.RotationAngleInDegrees), rotation);
        visual.StartAnimation(nameof(Visual.Opacity), opacity);
    }
}
