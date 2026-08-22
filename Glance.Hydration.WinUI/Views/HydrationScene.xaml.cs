using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Numerics;
using Windows.UI;

namespace Glance.Hydration.WinUI;

public sealed partial class HydrationScene :
    UserControl
{
    private bool? criticalPulseActive;
    private HydrationLevel? paletteLevel;
    private HydrationLevel previousLevel;
    private Visual? waterVisual;

    public HydrationScene() => InitializeComponent();

    public HydrationViewModel? ViewModel
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
        waterVisual = ElementCompositionPreview.GetElementVisual(WaterLayer);
        StartWaveMotion(BackWave, 11);
        StartWaveMotion(FrontWave, 8);
        UpdateScene(false);
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        SizeChanged -= HandleSizeChanged;
        Unsubscribe();
        StopWaveMotion(BackWave);
        StopWaveMotion(FrontWave);
        ElementCompositionPreview.GetElementVisual(CompletionRipple).StopAnimation(nameof(Visual.Opacity));
        ElementCompositionPreview.GetElementVisual(CompletionRipple).StopAnimation(nameof(Visual.Scale));
        waterVisual?.StopAnimation("Offset.Y");
        criticalPulseActive = null;
        waterVisual = null;
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
        if (args.PropertyName is nameof(HydrationViewModel.Progress) or nameof(HydrationViewModel.Level))
        {
            UpdateScene(true);
        }
    }

    private void UpdateScene(bool animate)
    {
        if (!IsLoaded || ViewModel is null || waterVisual is null || ActualHeight <= 0)
        {
            return;
        }

        ApplyPalette(ViewModel.Level);
        float target = (float)(Math.Clamp(1 - ViewModel.Progress, 0, 1) * ActualHeight) - 8;

        if (animate)
        {
            Compositor compositor = waterVisual.Compositor;
            ScalarKeyFrameAnimation animation = compositor.CreateScalarKeyFrameAnimation();
            animation.InsertExpressionKeyFrame(0, "this.StartingValue");
            animation.InsertKeyFrame(1, target, compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1)));
            animation.Duration = TimeSpan.FromMilliseconds(650);
            animation.StopBehavior = AnimationStopBehavior.SetToFinalValue;
            waterVisual.StartAnimation("Offset.Y", animation);
        }
        else
        {
            waterVisual.StopAnimation("Offset.Y");
            waterVisual.Offset = new Vector3(waterVisual.Offset.X, target, waterVisual.Offset.Z);
        }

        UpdateCriticalPulse(ViewModel.Level == HydrationLevel.Critical);

        if (ViewModel.Level == HydrationLevel.GoalReached && previousLevel != HydrationLevel.GoalReached)
        {
            StartCompletionRipple();
        }

        previousLevel = ViewModel.Level;
    }

    private void ApplyPalette(HydrationLevel level)
    {
        if (paletteLevel == level)
        {
            return;
        }

        (Color background, Color back, Color front) = level switch
        {
            HydrationLevel.Critical => (Color.FromArgb(255, 49, 34, 42), Color.FromArgb(180, 207, 111, 86), Color.FromArgb(230, 227, 125, 96)),
            HydrationLevel.Behind => (Color.FromArgb(255, 18, 39, 56), Color.FromArgb(170, 61, 145, 174), Color.FromArgb(225, 43, 160, 197)),
            HydrationLevel.GoalReached => (Color.FromArgb(255, 10, 47, 70), Color.FromArgb(180, 86, 207, 232), Color.FromArgb(235, 39, 181, 219)),
            _ => (Color.FromArgb(255, 16, 37, 61), Color.FromArgb(160, 94, 201, 232), Color.FromArgb(220, 41, 168, 209))
        };
        SceneRoot.Background = new SolidColorBrush(background);
        BackWave.Fill = new SolidColorBrush(back);
        FrontWave.Fill = new SolidColorBrush(front);
        paletteLevel = level;
    }

    private static void StartWaveMotion(FrameworkElement element, double seconds)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        ScalarKeyFrameAnimation animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        LinearEasingFunction easing = visual.Compositor.CreateLinearEasingFunction();
        animation.InsertKeyFrame(0, 0, easing);
        animation.InsertKeyFrame(1, -120, easing);
        animation.Duration = TimeSpan.FromSeconds(seconds);
        animation.IterationBehavior = AnimationIterationBehavior.Forever;
        visual.StartAnimation("Offset.X", animation);
    }

    private static void StopWaveMotion(FrameworkElement element) => ElementCompositionPreview.GetElementVisual(element).StopAnimation("Offset.X");

    private void UpdateCriticalPulse(bool active)
    {
        if (criticalPulseActive == active)
        {
            return;
        }

        criticalPulseActive = active;
        Visual visual = ElementCompositionPreview.GetElementVisual(FrontWave);
        visual.StopAnimation(nameof(Visual.Opacity));

        if (!active)
        {
            visual.Opacity = 1;
            return;
        }

        ScalarKeyFrameAnimation animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(0, 0.72f);
        animation.InsertKeyFrame(0.5f, 1);
        animation.InsertKeyFrame(1, 0.72f);
        animation.Duration = TimeSpan.FromSeconds(2.4);
        animation.IterationBehavior = AnimationIterationBehavior.Forever;
        visual.StartAnimation(nameof(Visual.Opacity), animation);
    }

    private void StartCompletionRipple()
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(CompletionRipple);
        visual.CenterPoint = new Vector3((float)CompletionRipple.ActualWidth / 2, (float)CompletionRipple.ActualHeight / 2, 0);
        visual.Scale = new Vector3(0.4f, 0.4f, 1);
        ScalarKeyFrameAnimation scale = visual.Compositor.CreateScalarKeyFrameAnimation();
        scale.InsertKeyFrame(0, 0.4f);
        scale.InsertKeyFrame(1, 1.8f);
        scale.Duration = TimeSpan.FromMilliseconds(750);
        ScalarKeyFrameAnimation opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0, 0.8f);
        opacity.InsertKeyFrame(1, 0);
        opacity.Duration = scale.Duration;
        visual.StartAnimation("Scale.X", scale);
        visual.StartAnimation("Scale.Y", scale);
        visual.StartAnimation(nameof(Visual.Opacity), opacity);
    }
}
