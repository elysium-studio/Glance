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
    private Visual? fireCoreVisual;
    private Visual? fireGlowVisual;
    private Visual? heatLayerVisual;

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
        Subscribe();
        fireCoreVisual = ElementCompositionPreview.GetElementVisual(FireCore);
        fireGlowVisual = ElementCompositionPreview.GetElementVisual(FireGlow);
        heatLayerVisual = ElementCompositionPreview.GetElementVisual(HeatLayer);
        completionLayerVisual = ElementCompositionPreview.GetElementVisual(CompletionLayer);
        StartFirePulse();
        StartEmber(ElementCompositionPreview.GetElementVisual(EmberOne), 0);
        StartEmber(ElementCompositionPreview.GetElementVisual(EmberTwo), 1.2);
        StartEmber(ElementCompositionPreview.GetElementVisual(EmberThree), 2.4);
        UpdateScene(false);
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        Unsubscribe();
        fireCoreVisual?.StopAnimation("Scale.X");
        fireCoreVisual?.StopAnimation("Scale.Y");
        fireGlowVisual?.StopAnimation("Scale.X");
        fireGlowVisual?.StopAnimation("Scale.Y");
        heatLayerVisual?.StopAnimation(nameof(Visual.Opacity));
        completionLayerVisual?.StopAnimation(nameof(Visual.Opacity));
        StopEmber(ElementCompositionPreview.GetElementVisual(EmberOne));
        StopEmber(ElementCompositionPreview.GetElementVisual(EmberTwo));
        StopEmber(ElementCompositionPreview.GetElementVisual(EmberThree));
        fireCoreVisual = null;
        fireGlowVisual = null;
        heatLayerVisual = null;
        completionLayerVisual = null;
    }

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
        if (!IsLoaded || ViewModel is null || fireCoreVisual is null || fireGlowVisual is null)
        {
            return;
        }

        float progress = (float)Math.Clamp(ViewModel.Progress, 0, 1);
        float activity = ViewModel.Stage == FastingStage.Ready ? 0.18f : 0.38f + progress * 0.62f;
        AnimateOpacity(heatLayerVisual, 0.08f + progress * 0.72f, animate);
        AnimateOpacity(completionLayerVisual, Math.Clamp((progress - 0.82f) * 4.4f, 0, 0.78f), animate);
        AnimateOpacity(fireCoreVisual, activity, animate);
        AnimateOpacity(fireGlowVisual, activity * 0.72f, animate);

        if (ViewModel.Stage == FastingStage.Completed && previousStage != FastingStage.Completed)
        {
            StartCompletionRing();
        }

        previousStage = ViewModel.Stage;
    }

    private void StartFirePulse()
    {
        if (fireCoreVisual is null || fireGlowVisual is null)
        {
            return;
        }

        StartPulse(fireCoreVisual, new Vector3(48, 92, 0), 0.96f, 1.04f, 2.8);
        StartPulse(fireGlowVisual, new Vector3(95, 95, 0), 0.92f, 1.08f, 3.6);
    }

    private static void StartPulse(Visual visual, Vector3 centerPoint, float minimum, float maximum, double seconds)
    {
        visual.CenterPoint = centerPoint;
        ScalarKeyFrameAnimation animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(0, minimum);
        animation.InsertKeyFrame(0.5f, maximum);
        animation.InsertKeyFrame(1, minimum);
        animation.Duration = TimeSpan.FromSeconds(seconds);
        animation.IterationBehavior = AnimationIterationBehavior.Forever;
        visual.StartAnimation("Scale.X", animation);
        visual.StartAnimation("Scale.Y", animation);
    }

    private static void StartEmber(Visual visual, double delaySeconds)
    {
        visual.Offset = new Vector3(visual.Offset.X, 34, visual.Offset.Z);
        ScalarKeyFrameAnimation rise = visual.Compositor.CreateScalarKeyFrameAnimation();
        rise.InsertKeyFrame(0, 34);
        rise.InsertKeyFrame(1, -54);
        rise.Duration = TimeSpan.FromSeconds(4.2);
        rise.DelayTime = TimeSpan.FromSeconds(delaySeconds);
        rise.IterationBehavior = AnimationIterationBehavior.Forever;
        ScalarKeyFrameAnimation opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0, 0);
        opacity.InsertKeyFrame(0.18f, 0.9f);
        opacity.InsertKeyFrame(0.72f, 0.45f);
        opacity.InsertKeyFrame(1, 0);
        opacity.Duration = rise.Duration;
        opacity.DelayTime = rise.DelayTime;
        opacity.IterationBehavior = AnimationIterationBehavior.Forever;
        visual.StartAnimation("Offset.Y", rise);
        visual.StartAnimation(nameof(Visual.Opacity), opacity);
    }

    private static void StopEmber(Visual visual)
    {
        visual.StopAnimation("Offset.Y");
        visual.StopAnimation(nameof(Visual.Opacity));
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
