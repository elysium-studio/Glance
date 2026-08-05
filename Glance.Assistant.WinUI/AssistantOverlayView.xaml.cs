using Glance.Application.Abstractions;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.ComponentModel;
using System.Numerics;

namespace Glance.Assistant.WinUI;

public sealed partial class AssistantOverlayView :
    UserControl,
    IGlanceAssistantConnectedAnimationView
{
    private readonly FrameworkElement[] energyBars;

    public AssistantOverlayView(IGlanceAssistantProvider provider)
    {
        Provider = provider;
        InitializeComponent();
        energyBars = [EnergyBar1, EnergyBar2, EnergyBar3, EnergyBar4];
    }

    public IGlanceAssistantProvider Provider { get; }

    public object ConnectedAnimationElement => AssistantGlyphSurface;

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        Provider.PropertyChanged += HandleProviderPropertyChanged;
        ConfigureVisuals();
        UpdateStateAnimations();
        AnimateTextSurface();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        Provider.PropertyChanged -= HandleProviderPropertyChanged;
        StopAnimations();
    }

    private void HandleProviderPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(IGlanceAssistantProvider.State))
        {
            _ = DispatcherQueue.TryEnqueue(UpdateStateAnimations);
        }

    }

    private void ConfigureVisuals()
    {
        SetCenterPoint(AssistantAmbience);
        SetCenterPoint(AssistantEnergyField);
        SetCenterPoint(OuterHalo);
        SetCenterPoint(PulseHalo);
        SetCenterPoint(Orbit);
        SetCenterPoint(OrbHighlight);
        ElementCompositionPreview.SetIsTranslationEnabled(TextSurface, true);

        foreach (FrameworkElement bar in energyBars)
        {
            Visual visual = ElementCompositionPreview.GetElementVisual(bar);
            visual.CenterPoint = new Vector3((float)bar.ActualWidth / 2, (float)bar.ActualHeight, 0);
        }
    }

    private void UpdateStateAnimations()
    {
        if (!IsLoaded)
        {
            return;
        }

        StopAnimations();
        bool isProcessing = Provider.State == GlanceAssistantState.ProcessingCommand;
        bool isListening = Provider.State == GlanceAssistantState.ListeningForCommand;

        AssistantGlyph.Glyph = isProcessing ? "\uE895" : "\uE720";
        EnergyBars.Visibility = isListening || isProcessing ? Visibility.Visible : Visibility.Collapsed;

        if (!isListening && !isProcessing)
        {
            SetRestingState();
            return;
        }

        StartHaloAnimation(isProcessing);
        StartAmbienceAnimation(isProcessing);
        StartOrbitAnimation(isProcessing);
        StartOrbBreathingAnimation(isProcessing);
        StartEnergyBarAnimations(isProcessing);

        if (isProcessing)
        {
            StartProcessingGlyphAnimation();
        }
    }

    private void StartHaloAnimation(bool isProcessing)
    {
        Visual outerVisual = ElementCompositionPreview.GetElementVisual(OuterHalo);
        Visual pulseVisual = ElementCompositionPreview.GetElementVisual(PulseHalo);
        Compositor compositor = outerVisual.Compositor;
        TimeSpan duration = TimeSpan.FromMilliseconds(isProcessing ? 820 : 1450);

        Vector3KeyFrameAnimation scale = compositor.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(0, new Vector3(0.84f, 0.84f, 1));
        scale.InsertKeyFrame(0.48f, Vector3.One, CreateEaseOut(compositor));
        scale.InsertKeyFrame(1, new Vector3(1.16f, 1.16f, 1), CreateEaseOut(compositor));
        scale.Duration = duration;
        scale.IterationBehavior = AnimationIterationBehavior.Forever;

        ScalarKeyFrameAnimation opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0, 0);
        opacity.InsertKeyFrame(0.28f, isProcessing ? 0.9f : 0.64f);
        opacity.InsertKeyFrame(1, 0);
        opacity.Duration = duration;
        opacity.IterationBehavior = AnimationIterationBehavior.Forever;
        outerVisual.StartAnimation(nameof(Visual.Scale), scale);
        outerVisual.StartAnimation(nameof(Visual.Opacity), opacity);

        Vector3KeyFrameAnimation innerScale = compositor.CreateVector3KeyFrameAnimation();
        innerScale.InsertKeyFrame(0, new Vector3(0.92f, 0.92f, 1));
        innerScale.InsertKeyFrame(0.5f, new Vector3(1.08f, 1.08f, 1), CreateEaseOut(compositor));
        innerScale.InsertKeyFrame(1, new Vector3(0.92f, 0.92f, 1), CreateEaseOut(compositor));
        innerScale.Duration = TimeSpan.FromMilliseconds(isProcessing ? 660 : 1100);
        innerScale.IterationBehavior = AnimationIterationBehavior.Forever;
        pulseVisual.Opacity = isProcessing ? 0.72f : 0.48f;
        pulseVisual.StartAnimation(nameof(Visual.Scale), innerScale);
    }

    private void StartAmbienceAnimation(bool isProcessing)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(AssistantAmbience);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.45f, 0),
            new Vector2(0.55f, 1));
        TimeSpan duration = TimeSpan.FromMilliseconds(isProcessing ? 2800 : 6200);
        Vector3KeyFrameAnimation scale = compositor.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(0, new Vector3(1.02f, 1.06f, 1));
        scale.InsertKeyFrame(0.5f, new Vector3(1.1f, 1.14f, 1), easing);
        scale.InsertKeyFrame(1, new Vector3(1.02f, 1.06f, 1), easing);
        scale.Duration = duration;
        scale.IterationBehavior = AnimationIterationBehavior.Forever;
        ScalarKeyFrameAnimation offset = compositor.CreateScalarKeyFrameAnimation();
        offset.InsertKeyFrame(0, -8);
        offset.InsertKeyFrame(0.5f, isProcessing ? 18 : 12, easing);
        offset.InsertKeyFrame(1, -8, easing);
        offset.Duration = duration;
        offset.IterationBehavior = AnimationIterationBehavior.Forever;
        ScalarKeyFrameAnimation opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0, isProcessing ? 0.68f : 0.52f);
        opacity.InsertKeyFrame(0.5f, isProcessing ? 0.86f : 0.7f, easing);
        opacity.InsertKeyFrame(1, isProcessing ? 0.68f : 0.52f, easing);
        opacity.Duration = duration;
        opacity.IterationBehavior = AnimationIterationBehavior.Forever;
        visual.StartAnimation(nameof(Visual.Scale), scale);
        visual.StartAnimation("Offset.X", offset);
        visual.StartAnimation(nameof(Visual.Opacity), opacity);
    }

    private void StartOrbitAnimation(bool isProcessing)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(Orbit);
        ScalarKeyFrameAnimation rotation = visual.Compositor.CreateScalarKeyFrameAnimation();
        rotation.InsertKeyFrame(0, 0);
        rotation.InsertKeyFrame(1, 360);
        rotation.Duration = TimeSpan.FromMilliseconds(isProcessing ? 1500 : 4200);
        rotation.IterationBehavior = AnimationIterationBehavior.Forever;
        visual.Opacity = isProcessing ? 1 : 0.78f;
        visual.StartAnimation(nameof(Visual.RotationAngleInDegrees), rotation);
    }

    private void StartOrbBreathingAnimation(bool isProcessing)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(OrbHighlight);
        Compositor compositor = visual.Compositor;
        Vector3KeyFrameAnimation scale = compositor.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(0, Vector3.One);
        scale.InsertKeyFrame(0.5f, new Vector3(isProcessing ? 1.34f : 1.18f, isProcessing ? 1.34f : 1.18f, 1), CreateEaseOut(compositor));
        scale.InsertKeyFrame(1, Vector3.One, CreateEaseOut(compositor));
        scale.Duration = TimeSpan.FromMilliseconds(isProcessing ? 720 : 1350);
        scale.IterationBehavior = AnimationIterationBehavior.Forever;
        visual.StartAnimation(nameof(Visual.Scale), scale);
    }

    private void StartEnergyBarAnimations(bool isProcessing)
    {
        for (int index = 0; index < energyBars.Length; index++)
        {
            Visual visual = ElementCompositionPreview.GetElementVisual(energyBars[index]);
            Vector3KeyFrameAnimation scale = visual.Compositor.CreateVector3KeyFrameAnimation();
            float low = 0.25f + (index * 0.06f);
            float high = isProcessing ? 1.18f - (index * 0.05f) : 0.78f + (index % 2 * 0.22f);
            scale.InsertKeyFrame(0, new Vector3(1, low, 1));
            scale.InsertKeyFrame(0.5f, new Vector3(1, high, 1), CreateEaseOut(visual.Compositor));
            scale.InsertKeyFrame(1, new Vector3(1, low, 1), CreateEaseOut(visual.Compositor));
            scale.Duration = TimeSpan.FromMilliseconds((isProcessing ? 380 : 620) + (index * 95));
            scale.IterationBehavior = AnimationIterationBehavior.Forever;
            visual.StartAnimation(nameof(Visual.Scale), scale);
        }
    }

    private void StartProcessingGlyphAnimation()
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(AssistantGlyph);
        SetCenterPoint(AssistantGlyph);
        ScalarKeyFrameAnimation rotation = visual.Compositor.CreateScalarKeyFrameAnimation();
        rotation.InsertKeyFrame(0, 0);
        rotation.InsertKeyFrame(1, 360);
        rotation.Duration = TimeSpan.FromMilliseconds(920);
        rotation.IterationBehavior = AnimationIterationBehavior.Forever;
        visual.StartAnimation(nameof(Visual.RotationAngleInDegrees), rotation);
    }

    private void AnimateTextSurface()
    {
        if (!IsLoaded)
        {
            return;
        }

        Visual visual = ElementCompositionPreview.GetElementVisual(TextSurface);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = CreateEaseOut(compositor);
        ScalarKeyFrameAnimation opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0, 0.42f);
        opacity.InsertKeyFrame(1, 1, easing);
        opacity.Duration = TimeSpan.FromMilliseconds(220);
        ScalarKeyFrameAnimation translation = compositor.CreateScalarKeyFrameAnimation();
        translation.InsertKeyFrame(0, 10);
        translation.InsertKeyFrame(1, 0, easing);
        translation.Duration = TimeSpan.FromMilliseconds(280);
        visual.StartAnimation(nameof(Visual.Opacity), opacity);
        visual.StartAnimation("Translation.X", translation);
    }

    private void StopAnimations()
    {
        StopVisualAnimations(AssistantAmbience);
        StopVisualAnimations(OuterHalo);
        StopVisualAnimations(PulseHalo);
        StopVisualAnimations(Orbit);
        StopVisualAnimations(OrbHighlight);
        StopVisualAnimations(AssistantGlyph);

        foreach (FrameworkElement bar in energyBars)
        {
            StopVisualAnimations(bar);
        }
    }

    private void SetRestingState()
    {
        Visual ambienceVisual = ElementCompositionPreview.GetElementVisual(AssistantAmbience);
        ambienceVisual.Opacity = 0.36f;
        Visual outerVisual = ElementCompositionPreview.GetElementVisual(OuterHalo);
        outerVisual.Opacity = 0.2f;
        Visual pulseVisual = ElementCompositionPreview.GetElementVisual(PulseHalo);
        pulseVisual.Opacity = 0.22f;
        Visual orbitVisual = ElementCompositionPreview.GetElementVisual(Orbit);
        orbitVisual.Opacity = 0.38f;
    }

    private static void StopVisualAnimations(FrameworkElement element)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation(nameof(Visual.Opacity));
        visual.StopAnimation(nameof(Visual.Scale));
        visual.StopAnimation(nameof(Visual.RotationAngleInDegrees));
        visual.StopAnimation("Offset.X");
        visual.Opacity = 1;
        visual.Scale = Vector3.One;
        visual.RotationAngleInDegrees = 0;
    }

    private static void SetCenterPoint(FrameworkElement element)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new Vector3((float)element.ActualWidth / 2, (float)element.ActualHeight / 2, 0);
    }

    private static CubicBezierEasingFunction CreateEaseOut(Compositor compositor) => compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1), new Vector2(0.3f, 1));
}
