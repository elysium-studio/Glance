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
    private const int IncomingDelayMs = OutgoingDurationMs;
    private const int IncomingDurationMs = 320;
    private const int OutgoingDurationMs = 260;

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(ImageSource),
            typeof(MediaArtworkTransition), new PropertyMetadata(null, HandleSourceChanged));

    private Image currentImage;
    private Image nextImage;
    private Visual? currentVisual;
    private Visual? nextVisual;
    private CompositionEasingFunction? easing;
    private ImageSource? displayedSource;
    private EventHandler<object>? preparationRenderingHandler;
    private int transitionGeneration;
    private bool isPreparing;
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

    private static void HandleSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((MediaArtworkTransition)sender).UpdateSource((ImageSource?)args.NewValue);

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        Visual perspectiveVisual = ElementCompositionPreview.GetElementVisual(PerspectiveLayer);
        Compositor compositor = perspectiveVisual.Compositor;
        Matrix4x4 perspective = Matrix4x4.Identity;
        perspective.M34 = -1f / 350f;
        perspectiveVisual.TransformMatrix = perspective;
        easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.18f, 0.86f), new Vector2(0.2f, 1));
        UpdateVisuals();
        CancelPreparation();
        StopAnimations();
        currentImage.Source = Source;
        nextImage.Source = null;
        Canvas.SetZIndex(currentImage, 1);
        Canvas.SetZIndex(nextImage, 0);
        currentVisual!.RotationAngleInDegrees = 0;
        currentVisual.Opacity = Source is null ? 0 : 1;
        currentVisual.Scale = Vector3.One;
        nextVisual!.RotationAngleInDegrees = 0;
        nextVisual.Opacity = 0;
        nextVisual.Scale = Vector3.One;
        displayedSource = Source;
        isPreparing = false;
        isTransitioning = false;
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        transitionGeneration++;
        CancelPreparation();
        StopAnimations();
        currentVisual = null;
        nextVisual = null;
        easing = null;
        isPreparing = false;
        isTransitioning = false;
    }

    private void HandleSizeChanged(object sender, SizeChangedEventArgs args) => UpdateCenters(args.NewSize);

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

        _ = currentVisual?.CenterPoint = center;

        _ = nextVisual?.CenterPoint = center;
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
        CancelPreparation();
        StopAnimations();

        if (source is null)
        {
            nextImage.Source = null;
            CompositionScopedBatch fadeOutBatch = currentVisual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            StartScalarAnimation(currentVisual, nameof(Visual.RotationAngleInDegrees), -90, OutgoingDurationMs);
            StartOutgoingOpacityAnimation(currentVisual);
            StartScaleAnimation(currentVisual, new Vector3(0.94f, 0.94f, 1), OutgoingDurationMs);
            fadeOutBatch.Completed += (_, _) =>
            {
                fadeOutBatch.Dispose();

                if (generation == transitionGeneration)
                {
                    currentImage.Source = null;
                    currentVisual.RotationAngleInDegrees = 0;
                    currentVisual.Opacity = 0;
                    currentVisual.Scale = Vector3.One;
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
            currentVisual.Scale = Vector3.One;
            return;
        }

        PrepareTransition(source, generation);
    }

    private void PrepareTransition(ImageSource source, int generation)
    {
        Image preparedImage = nextImage;
        Visual preparedVisual = nextVisual!;
        Visual visibleVisual = currentVisual!;

        isPreparing = true;
        Canvas.SetZIndex(currentImage, 0);
        Canvas.SetZIndex(preparedImage, 1);
        preparedVisual.RotationAngleInDegrees = 90;
        preparedVisual.Opacity = 0;
        preparedVisual.Scale = new Vector3(0.94f, 0.94f, 1);
        visibleVisual.RotationAngleInDegrees = 0;
        visibleVisual.Opacity = 1;
        visibleVisual.Scale = Vector3.One;
        preparedImage.Source = source;
        preparationRenderingHandler = (_, _) =>
        {
            CancelPreparationHandler();

            if (generation == transitionGeneration &&
                ReferenceEquals(preparedImage, nextImage) &&
                ReferenceEquals(preparedImage.Source, displayedSource))
            {
                BeginTransition(generation);
            }
        };
        CompositionTarget.Rendering += preparationRenderingHandler;
    }

    private void BeginTransition(int generation)
    {
        if (!isPreparing || currentVisual is null || nextVisual is null)
        {
            return;
        }

        isPreparing = false;
        isTransitioning = true;

        CompositionScopedBatch flipBatch = currentVisual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        StartScalarAnimation(currentVisual, nameof(Visual.RotationAngleInDegrees), -90, OutgoingDurationMs);
        StartOutgoingOpacityAnimation(currentVisual);
        StartScaleAnimation(currentVisual, new Vector3(0.94f, 0.94f, 1), OutgoingDurationMs);
        StartScalarAnimation(nextVisual, nameof(Visual.RotationAngleInDegrees), 0, IncomingDurationMs, IncomingDelayMs);
        StartIncomingOpacityAnimation(nextVisual);
        StartScaleAnimation(nextVisual, Vector3.One, IncomingDurationMs, IncomingDelayMs);
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
        currentVisual.Scale = Vector3.One;
        nextVisual!.RotationAngleInDegrees = 0;
        nextVisual.Opacity = nextImage.Source is null ? 0 : 1;
        nextVisual.Scale = Vector3.One;
        (currentImage, nextImage) = (nextImage, currentImage);
        (currentVisual, nextVisual) = (nextVisual, currentVisual);
        isPreparing = false;
        isTransitioning = false;
    }

    private void CancelPreparation()
    {
        CancelPreparationHandler();
        isPreparing = false;
    }

    private void CancelPreparationHandler()
    {
        if (preparationRenderingHandler is null)
        {
            return;
        }

        CompositionTarget.Rendering -= preparationRenderingHandler;
        preparationRenderingHandler = null;
    }

    private void StopAnimations()
    {
        currentVisual?.StopAnimation(nameof(Visual.RotationAngleInDegrees));
        currentVisual?.StopAnimation(nameof(Visual.Opacity));
        currentVisual?.StopAnimation(nameof(Visual.Scale));
        nextVisual?.StopAnimation(nameof(Visual.RotationAngleInDegrees));
        nextVisual?.StopAnimation(nameof(Visual.Opacity));
        nextVisual?.StopAnimation(nameof(Visual.Scale));
    }

    private void StartOutgoingOpacityAnimation(Visual visual)
    {
        ScalarKeyFrameAnimation animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(0.82f, 1);
        animation.InsertKeyFrame(1, 0);
        animation.Duration = TimeSpan.FromMilliseconds(OutgoingDurationMs);
        visual.StartAnimation(nameof(Visual.Opacity), animation);
    }

    private void StartIncomingOpacityAnimation(Visual visual)
    {
        ScalarKeyFrameAnimation animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(0, 0);
        animation.InsertKeyFrame(0.08f, 1);
        animation.InsertKeyFrame(1, 1);
        animation.Duration = TimeSpan.FromMilliseconds(IncomingDurationMs);
        animation.DelayTime = TimeSpan.FromMilliseconds(IncomingDelayMs);
        visual.StartAnimation(nameof(Visual.Opacity), animation);
    }

    private void StartScaleAnimation(Visual visual, Vector3 scale, int durationMs, int delayMs = 0)
    {
        Vector3KeyFrameAnimation animation = visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.InsertKeyFrame(1, scale, easing);
        animation.Duration = TimeSpan.FromMilliseconds(durationMs);
        animation.DelayTime = TimeSpan.FromMilliseconds(delayMs);
        visual.StartAnimation(nameof(Visual.Scale), animation);
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
