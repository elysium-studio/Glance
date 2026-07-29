using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Numerics;

namespace Glance.Media.WinUI;

public sealed partial class MediaArtworkTransition :
    UserControl
{
    private const int IncomingDelayMs = 130;
    private const int IncomingDurationMs = 230;
    private const int OutgoingDurationMs = 180;

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(ImageSource),
            typeof(MediaArtworkTransition), new PropertyMetadata(null, HandleSourceChanged));

    private Image currentImage;
    private Image nextImage;
    private Visual? currentVisual;
    private Visual? nextVisual;
    private CompositionEasingFunction? easing;
    private ImageSource? displayedSource;
    private int transitionGeneration;
    private bool isTransitioning;

    public MediaArtworkTransition()
    {
        InitializeComponent();
        currentImage = PrimaryArtwork;
        nextImage = SecondaryArtwork;
    }

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    private static void HandleSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((MediaArtworkTransition)sender).UpdateSource((ImageSource?)args.NewValue);

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        Visual perspectiveVisual = ElementCompositionPreview.GetElementVisual(PerspectiveLayer);
        Compositor compositor = perspectiveVisual.Compositor;
        Matrix4x4 perspective = Matrix4x4.Identity;
        perspective.M34 = -1f / 500f;
        perspectiveVisual.TransformMatrix = perspective;
        easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.82f), new Vector2(0.2f, 1));
        UpdateVisuals();
        currentVisual!.RotationAngleInDegrees = 0;
        currentVisual.Opacity = currentImage.Source is null ? 0 : 1;
        nextVisual!.RotationAngleInDegrees = 0;
        nextVisual.Opacity = 0;
        displayedSource = Source;
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        transitionGeneration++;
        StopAnimations();
        currentVisual = null;
        nextVisual = null;
        easing = null;
        isTransitioning = false;
    }

    private void HandleSizeChanged(object sender, SizeChangedEventArgs args) =>
        UpdateCenters(args.NewSize);

    private void UpdateVisuals()
    {
        currentVisual = ElementCompositionPreview.GetElementVisual(currentImage);
        nextVisual = ElementCompositionPreview.GetElementVisual(nextImage);
        currentVisual.RotationAxis = Vector3.UnitY;
        nextVisual.RotationAxis = Vector3.UnitY;
        UpdateCenters(new Windows.Foundation.Size(ActualWidth, ActualHeight));
    }

    private void UpdateCenters(Windows.Foundation.Size size)
    {
        Vector3 center = new((float)size.Width / 2, (float)size.Height / 2, 0);

        if (currentVisual is not null)
        {
            currentVisual.CenterPoint = center;
        }

        if (nextVisual is not null)
        {
            nextVisual.CenterPoint = center;
        }
    }

    private void UpdateSource(ImageSource? source)
    {
        if (ReferenceEquals(displayedSource, source))
        {
            return;
        }

        displayedSource = source;

        if (currentVisual is null || nextVisual is null || easing is null)
        {
            currentImage.Source = source;
            return;
        }

        if (isTransitioning)
        {
            FinishTransition();
        }

        int generation = ++transitionGeneration;

        if (source is null)
        {
            CompositionScopedBatch fadeOutBatch = currentVisual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            StartScalarAnimation(currentVisual, nameof(Visual.RotationAngleInDegrees), -88, OutgoingDurationMs);
            StartScalarAnimation(currentVisual, nameof(Visual.Opacity), 0, OutgoingDurationMs);
            fadeOutBatch.Completed += (_, _) =>
            {
                fadeOutBatch.Dispose();

                if (generation == transitionGeneration)
                {
                    currentImage.Source = null;
                    currentVisual.RotationAngleInDegrees = 0;
                    currentVisual.Opacity = 0;
                }
            };
            fadeOutBatch.End();
            return;
        }

        if (currentImage.Source is null)
        {
            currentImage.Source = source;
            currentVisual.RotationAngleInDegrees = 0;
            currentVisual.Opacity = 1;
            return;
        }

        nextImage.Source = source;
        nextVisual.RotationAngleInDegrees = 88;
        nextVisual.Opacity = 0;
        isTransitioning = true;

        CompositionScopedBatch flipBatch = currentVisual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        StartScalarAnimation(currentVisual, nameof(Visual.RotationAngleInDegrees), -88, OutgoingDurationMs);
        StartScalarAnimation(currentVisual, nameof(Visual.Opacity), 0, OutgoingDurationMs);
        StartScalarAnimation(nextVisual, nameof(Visual.RotationAngleInDegrees), 0, IncomingDurationMs, IncomingDelayMs);
        StartScalarAnimation(nextVisual, nameof(Visual.Opacity), 1, IncomingDurationMs, IncomingDelayMs);
        flipBatch.Completed += (_, _) =>
        {
            flipBatch.Dispose();

            if (generation == transitionGeneration)
            {
                FinishTransition();
            }
        };
        flipBatch.End();
    }

    private void FinishTransition()
    {
        StopAnimations();
        currentImage.Source = null;
        currentVisual!.RotationAngleInDegrees = 0;
        currentVisual.Opacity = 0;
        nextVisual!.RotationAngleInDegrees = 0;
        nextVisual.Opacity = nextImage.Source is null ? 0 : 1;
        (currentImage, nextImage) = (nextImage, currentImage);
        (currentVisual, nextVisual) = (nextVisual, currentVisual);
        isTransitioning = false;
    }

    private void StopAnimations()
    {
        currentVisual?.StopAnimation(nameof(Visual.RotationAngleInDegrees));
        currentVisual?.StopAnimation(nameof(Visual.Opacity));
        nextVisual?.StopAnimation(nameof(Visual.RotationAngleInDegrees));
        nextVisual?.StopAnimation(nameof(Visual.Opacity));
    }

    private void StartScalarAnimation(Visual visual, string property, float value, int durationMs, int delayMs = 0)
    {
        ScalarKeyFrameAnimation animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(1, value, easing);
        animation.Duration = TimeSpan.FromMilliseconds(durationMs);
        animation.DelayTime = TimeSpan.FromMilliseconds(delayMs);
        visual.StartAnimation(property, animation);
    }
}
