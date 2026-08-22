using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.ComponentModel;
using System.Numerics;

namespace Glance.Fasting.WinUI;

public sealed partial class FastingScene :
    UserControl
{
    private FastingStage previousStage;
    private Visual? completionLayerVisual;
    private Visual? dawnLayerVisual;
    private Visual? glowVisual;
    private Visual? orbVisual;

    public FastingScene() => InitializeComponent();

    public FastingViewModel? ViewModel
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            Unsubscribe();
            field = value;
            Subscribe();
            UpdateScene(false);
        }
    }

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        SizeChanged += HandleSizeChanged;
        Subscribe();
        orbVisual = ElementCompositionPreview.GetElementVisual(ProgressOrb);
        glowVisual = ElementCompositionPreview.GetElementVisual(GlowOrb);
        dawnLayerVisual = ElementCompositionPreview.GetElementVisual(DawnLayer);
        completionLayerVisual = ElementCompositionPreview.GetElementVisual(CompletionLayer);
        StartGlowPulse();
        UpdateScene(false);
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        SizeChanged -= HandleSizeChanged;
        Unsubscribe();
        orbVisual?.StopAnimation("Offset.X");
        orbVisual?.StopAnimation("Offset.Y");
        glowVisual?.StopAnimation("Offset.X");
        glowVisual?.StopAnimation("Offset.Y");
        glowVisual?.StopAnimation("Scale.X");
        glowVisual?.StopAnimation("Scale.Y");
        dawnLayerVisual?.StopAnimation(nameof(Visual.Opacity));
        completionLayerVisual?.StopAnimation(nameof(Visual.Opacity));
        orbVisual = null;
        glowVisual = null;
        dawnLayerVisual = null;
        completionLayerVisual = null;
    }

    private void HandleSizeChanged(object sender, SizeChangedEventArgs args) => UpdateScene(false);

    private void Subscribe()
    {
        if (!IsLoaded || ViewModel is null)
        {
            return;
        }

        ViewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        ViewModel.PropertyChanged += HandleViewModelPropertyChanged;
    }

    private void Unsubscribe() => ViewModel?.PropertyChanged -= HandleViewModelPropertyChanged;

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(FastingViewModel.Progress) or nameof(FastingViewModel.Stage))
        {
            UpdateScene(true);
        }
    }

    private void UpdateScene(bool animate)
    {
        if (!IsLoaded || ViewModel is null || orbVisual is null || glowVisual is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        double progress = Math.Clamp(ViewModel.Progress, 0, 1);
        float targetX = (float)(progress * Math.Max(0, ActualWidth - 96));
        float targetY = (float)(-Math.Sin(progress * Math.PI) * Math.Max(18, ActualHeight * 0.48));
        MoveVisual(orbVisual, targetX, targetY, animate);
        MoveVisual(glowVisual, targetX, targetY, animate);
        AnimateOpacity(dawnLayerVisual, (float)Math.Clamp(progress * 1.4, 0, 0.88), animate);
        AnimateOpacity(completionLayerVisual, (float)Math.Clamp((progress - 0.68) * 3.125, 0, 0.92), animate);

        if (ViewModel.Stage == FastingStage.Completed && previousStage != FastingStage.Completed)
        {
            StartCompletionRing();
        }

        previousStage = ViewModel.Stage;
    }

    private static void MoveVisual(Visual visual, float targetX, float targetY, bool animate)
    {
        if (!animate)
        {
            visual.StopAnimation("Offset.X");
            visual.StopAnimation("Offset.Y");
            visual.Offset = new Vector3(targetX, targetY, visual.Offset.Z);
            return;
        }

        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1));
        ScalarKeyFrameAnimation horizontal = compositor.CreateScalarKeyFrameAnimation();
        horizontal.InsertExpressionKeyFrame(0, "this.StartingValue");
        horizontal.InsertKeyFrame(1, targetX, easing);
        horizontal.Duration = TimeSpan.FromMilliseconds(900);
        horizontal.StopBehavior = AnimationStopBehavior.SetToFinalValue;
        ScalarKeyFrameAnimation vertical = compositor.CreateScalarKeyFrameAnimation();
        vertical.InsertExpressionKeyFrame(0, "this.StartingValue");
        vertical.InsertKeyFrame(1, targetY, easing);
        vertical.Duration = horizontal.Duration;
        vertical.StopBehavior = AnimationStopBehavior.SetToFinalValue;
        visual.StartAnimation("Offset.X", horizontal);
        visual.StartAnimation("Offset.Y", vertical);
    }

    private static void AnimateOpacity(Visual? visual, float target, bool animate)
    {
        if (visual is null)
        {
            return;
        }

        if (!animate)
        {
            visual.StopAnimation(nameof(Visual.Opacity));
            visual.Opacity = target;
            return;
        }

        ScalarKeyFrameAnimation animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.InsertExpressionKeyFrame(0, "this.StartingValue");
        animation.InsertKeyFrame(1, target);
        animation.Duration = TimeSpan.FromMilliseconds(900);
        animation.StopBehavior = AnimationStopBehavior.SetToFinalValue;
        visual.StartAnimation(nameof(Visual.Opacity), animation);
    }

    private void StartGlowPulse()
    {
        if (glowVisual is null)
        {
            return;
        }

        glowVisual.CenterPoint = new Vector3(56, 56, 0);
        ScalarKeyFrameAnimation animation = glowVisual.Compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(0, 0.92f);
        animation.InsertKeyFrame(0.5f, 1.08f);
        animation.InsertKeyFrame(1, 0.92f);
        animation.Duration = TimeSpan.FromSeconds(3.8);
        animation.IterationBehavior = AnimationIterationBehavior.Forever;
        glowVisual.StartAnimation("Scale.X", animation);
        glowVisual.StartAnimation("Scale.Y", animation);
    }

    private void StartCompletionRing()
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(CompletionRing);
        visual.CenterPoint = new Vector3(35, 35, 0);
        visual.Scale = new Vector3(0.5f, 0.5f, 1);
        ScalarKeyFrameAnimation scale = visual.Compositor.CreateScalarKeyFrameAnimation();
        scale.InsertKeyFrame(0, 0.5f);
        scale.InsertKeyFrame(1, 2.2f);
        scale.Duration = TimeSpan.FromMilliseconds(850);
        ScalarKeyFrameAnimation opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0, 0.9f);
        opacity.InsertKeyFrame(1, 0);
        opacity.Duration = scale.Duration;
        visual.StartAnimation("Scale.X", scale);
        visual.StartAnimation("Scale.Y", scale);
        visual.StartAnimation(nameof(Visual.Opacity), opacity);
    }
}
