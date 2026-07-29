using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;

namespace Glance.Media.WinUI;

public sealed partial class MediaAlbumAmbience :
    UserControl
{
    private const int ArtworkTransitionDurationMs = 560;

    private MediaViewModel? viewModel;
    private Visual? ambientVisual;
    private Image currentArtworkImage;
    private Image nextArtworkImage;
    private Visual? currentArtworkVisual;
    private Visual? motionVisual;
    private Visual? nextArtworkVisual;
    private CompositionRoundedRectangleGeometry? clipGeometry;
    private CompositionGeometricClip? roundedClip;
    private CompositionEasingFunction? easing;
    private ImplicitAnimationCollection? motionImplicitAnimations;
    private ImplicitAnimationCollection? ambientImplicitAnimations;
    private ImageSource? currentArtwork;
    private EventHandler<object>? artworkPreparationRenderingHandler;
    private int artworkTransitionGeneration;
    private bool isArtworkPreparing;
    private bool isArtworkTransitioning;
    private bool isPanning;

    public MediaAlbumAmbience()
    {
        InitializeComponent();
        currentArtworkImage = PrimaryArtwork;
        nextArtworkImage = SecondaryArtwork;
    }

    public MediaViewModel? ViewModel
    {
        get => viewModel;
        set
        {
            if (ReferenceEquals(viewModel, value))
            {
                return;
            }

            Unsubscribe();
            viewModel = value;
            Subscribe();
            UpdateArtwork();
            UpdateState();
        }
    }

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        Compositor compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        ambientVisual = ElementCompositionPreview.GetElementVisual(this);
        motionVisual = ElementCompositionPreview.GetElementVisual(MotionLayer);
        currentArtworkVisual = ElementCompositionPreview.GetElementVisual(currentArtworkImage);
        nextArtworkVisual = ElementCompositionPreview.GetElementVisual(nextArtworkImage);
        ambientVisual.Opacity = 0;
        currentArtworkVisual.Opacity = currentArtworkImage.Source is null ? 0 : 1;
        nextArtworkVisual.Opacity = 0;
        clipGeometry = compositor.CreateRoundedRectangleGeometry();
        clipGeometry.CornerRadius = new Vector2(28);
        clipGeometry.Size = new Vector2((float)ActualWidth, (float)ActualHeight);
        roundedClip = compositor.CreateGeometricClip(clipGeometry);
        ambientVisual.Clip = roundedClip;
        motionVisual.CenterPoint = new Vector3((float)ActualWidth / 2, (float)ActualHeight / 2, 0);
        easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.22f, 0.72f), new Vector2(0.18f, 1));
        Subscribe();
        UpdateArtwork();
        ConfigureResponseAnimations(compositor);
        UpdateState();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        Unsubscribe();
        StopPanning();

        if (motionVisual is not null)
        {
            motionVisual.ImplicitAnimations = null;
        }

        if (ambientVisual is not null)
        {
            ambientVisual.ImplicitAnimations = null;
        }

        motionImplicitAnimations?.Dispose();
        ambientImplicitAnimations?.Dispose();
        roundedClip?.Dispose();
        clipGeometry?.Dispose();
        roundedClip = null;
        clipGeometry = null;
        motionImplicitAnimations = null;
        ambientImplicitAnimations = null;
        CancelArtworkPreparation();
        ambientVisual = null;
        currentArtworkVisual = null;
        motionVisual = null;
        nextArtworkVisual = null;
        easing = null;
        artworkTransitionGeneration++;
        isArtworkPreparing = false;
        isArtworkTransitioning = false;
    }

    private void HandleSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (motionVisual is not null)
        {
            motionVisual.CenterPoint = new Vector3((float)args.NewSize.Width / 2, (float)args.NewSize.Height / 2, 0);
        }

        if (clipGeometry is not null)
        {
            clipGeometry.Size = new Vector2((float)args.NewSize.Width, (float)args.NewSize.Height);
        }
    }

    private void Subscribe()
    {
        if (!IsLoaded || viewModel is null)
        {
            return;
        }

        viewModel.PropertyChanged -= HandlePropertyChanged;
        viewModel.PropertyChanged += HandlePropertyChanged;
        viewModel.AudioLevelsChanged -= HandleAudioLevelsChanged;
        viewModel.AudioLevelsChanged += HandleAudioLevelsChanged;
    }

    private void Unsubscribe()
    {
        if (viewModel is null)
        {
            return;
        }

        viewModel.PropertyChanged -= HandlePropertyChanged;
        viewModel.AudioLevelsChanged -= HandleAudioLevelsChanged;
    }

    private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MediaViewModel.AmbientArtwork))
        {
            UpdateArtwork();
        }

        if (args.PropertyName is nameof(MediaViewModel.AmbientArtwork) or
            nameof(MediaViewModel.HasSession) or
            nameof(MediaViewModel.IsPlaying) or
            nameof(MediaViewModel.ShowAudioVisualization))
        {
            UpdateState();
        }
    }

    private void HandleAudioLevelsChanged(object? sender, AudioLevelsChangedEventArgs args)
    {
        if (!CanAnimate || motionVisual is null || ambientVisual is null || easing is null)
        {
            return;
        }

        double bass = Average(args.Levels, 0, 2);
        double energy = Average(args.Levels, 0, args.Levels.Count);

        float scale = (float)(1.38 + (bass * 0.035));
        float opacity = (float)(0.92 + (energy * 0.025));

        ApplyResponse(scale, opacity);
    }

    private void UpdateArtwork()
    {
        ImageSource? artwork = viewModel?.AmbientArtwork as ImageSource;

        if (ReferenceEquals(currentArtwork, artwork))
        {
            return;
        }

        currentArtwork = artwork;

        if (currentArtworkVisual is null || nextArtworkVisual is null || easing is null)
        {
            currentArtworkImage.Source = artwork;
            return;
        }

        if (isArtworkTransitioning)
        {
            FinishArtworkTransition();
        }

        int transitionGeneration = ++artworkTransitionGeneration;
        CancelArtworkPreparation();
        currentArtworkVisual.StopAnimation(nameof(Visual.Opacity));
        nextArtworkVisual.StopAnimation(nameof(Visual.Opacity));

        if (artwork is null)
        {
            nextArtworkImage.Source = null;
            CompositionScopedBatch fadeOutBatch = currentArtworkVisual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            StartArtworkOpacityAnimation(currentArtworkVisual, 0, ArtworkTransitionDurationMs);
            fadeOutBatch.Completed += (_, _) =>
            {
                fadeOutBatch.Dispose();

                if (transitionGeneration == artworkTransitionGeneration)
                {
                    currentArtworkImage.Source = null;
                    currentArtworkVisual.Opacity = 0;
                }
            };
            fadeOutBatch.End();
            return;
        }

        if (currentArtworkImage.Source is null)
        {
            currentArtworkImage.Source = artwork;
            currentArtworkVisual.Opacity = 1;
            nextArtworkVisual.Opacity = 0;
            return;
        }

        PrepareArtworkTransition(artwork, transitionGeneration);
    }

    private void PrepareArtworkTransition(ImageSource artwork, int transitionGeneration)
    {
        Image preparedImage = nextArtworkImage;

        isArtworkPreparing = true;
        nextArtworkVisual!.Opacity = 0;
        currentArtworkVisual!.Opacity = 1;
        preparedImage.Source = artwork;
        artworkPreparationRenderingHandler = (_, _) =>
        {
            CancelArtworkPreparationHandler();

            if (transitionGeneration == artworkTransitionGeneration &&
                ReferenceEquals(preparedImage, nextArtworkImage) &&
                ReferenceEquals(preparedImage.Source, currentArtwork))
            {
                BeginArtworkTransition(transitionGeneration);
            }
        };
        CompositionTarget.Rendering += artworkPreparationRenderingHandler;
    }

    private void BeginArtworkTransition(int transitionGeneration)
    {
        if (!isArtworkPreparing || currentArtworkVisual is null || nextArtworkVisual is null)
        {
            return;
        }

        isArtworkPreparing = false;
        isArtworkTransitioning = true;

        CompositionScopedBatch transition = nextArtworkVisual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        StartArtworkOpacityAnimation(nextArtworkVisual, 1, ArtworkTransitionDurationMs);
        transition.Completed += (_, _) =>
        {
            transition.Dispose();

            if (transitionGeneration == artworkTransitionGeneration)
            {
                FinishArtworkTransition();
            }
        };
        transition.End();
    }

    private void FinishArtworkTransition()
    {
        currentArtworkVisual!.StopAnimation(nameof(Visual.Opacity));
        nextArtworkVisual!.StopAnimation(nameof(Visual.Opacity));
        currentArtworkImage.Source = null;
        currentArtworkVisual.Opacity = 0;
        nextArtworkVisual.Opacity = nextArtworkImage.Source is null ? 0 : 1;
        (currentArtworkImage, nextArtworkImage) = (nextArtworkImage, currentArtworkImage);
        (currentArtworkVisual, nextArtworkVisual) = (nextArtworkVisual, currentArtworkVisual);
        isArtworkPreparing = false;
        isArtworkTransitioning = false;
    }

    private void CancelArtworkPreparation()
    {
        CancelArtworkPreparationHandler();
        isArtworkPreparing = false;
    }

    private void CancelArtworkPreparationHandler()
    {
        if (artworkPreparationRenderingHandler is null)
        {
            return;
        }

        CompositionTarget.Rendering -= artworkPreparationRenderingHandler;
        artworkPreparationRenderingHandler = null;
    }

    private void UpdateState()
    {
        if (ambientVisual is null || motionVisual is null || easing is null)
        {
            return;
        }

        bool hasArtwork = viewModel?.HasSession == true && viewModel.AmbientArtwork is ImageSource;
        float opacity = hasArtwork ? 0.92f : 0;

        if (ShouldPan)
        {
            StartPanning();
        }
        else
        {
            StopPanning();
        }

        if (!CanAnimate)
        {
            ApplyResponse(1.38f, opacity);
            return;
        }

        ApplyResponse(1.38f, opacity);
    }

    private void ConfigureResponseAnimations(Compositor compositor)
    {
        if (motionVisual is null || ambientVisual is null || easing is null)
        {
            return;
        }

        Vector3KeyFrameAnimation scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.Target = nameof(Visual.Scale);
        scaleAnimation.Duration = TimeSpan.FromMilliseconds(220);
        scaleAnimation.InsertExpressionKeyFrame(1, "this.FinalValue", easing);
        motionImplicitAnimations = compositor.CreateImplicitAnimationCollection();
        motionImplicitAnimations[nameof(Visual.Scale)] = scaleAnimation;
        motionVisual.ImplicitAnimations = motionImplicitAnimations;

        ScalarKeyFrameAnimation opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.Target = nameof(Visual.Opacity);
        opacityAnimation.Duration = TimeSpan.FromMilliseconds(280);
        opacityAnimation.InsertExpressionKeyFrame(1, "this.FinalValue", easing);
        ambientImplicitAnimations = compositor.CreateImplicitAnimationCollection();
        ambientImplicitAnimations[nameof(Visual.Opacity)] = opacityAnimation;
        ambientVisual.ImplicitAnimations = ambientImplicitAnimations;
    }

    private void StartPanning()
    {
        if (motionVisual is null || isPanning)
        {
            return;
        }

        Compositor compositor = motionVisual.Compositor;
        CompositionEasingFunction panEasing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.45f, 0), new Vector2(0.55f, 1));
        ScalarKeyFrameAnimation horizontalAnimation = compositor.CreateScalarKeyFrameAnimation();
        horizontalAnimation.Duration = TimeSpan.FromSeconds(12);
        horizontalAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        horizontalAnimation.InsertKeyFrame(0, -14, panEasing);
        horizontalAnimation.InsertKeyFrame(0.5f, 14, panEasing);
        horizontalAnimation.InsertKeyFrame(1, -14, panEasing);
        motionVisual.StartAnimation("Offset.X", horizontalAnimation);

        ScalarKeyFrameAnimation verticalAnimation = compositor.CreateScalarKeyFrameAnimation();
        verticalAnimation.Duration = TimeSpan.FromSeconds(15);
        verticalAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        verticalAnimation.InsertKeyFrame(0, 0, panEasing);
        verticalAnimation.InsertKeyFrame(0.25f, -9, panEasing);
        verticalAnimation.InsertKeyFrame(0.75f, 9, panEasing);
        verticalAnimation.InsertKeyFrame(1, 0, panEasing);
        motionVisual.StartAnimation("Offset.Y", verticalAnimation);
        isPanning = true;
    }

    private void StopPanning()
    {
        if (motionVisual is null || !isPanning)
        {
            return;
        }

        motionVisual.StopAnimation("Offset.X");
        motionVisual.StopAnimation("Offset.Y");
        motionVisual.Offset = Vector3.Zero;
        isPanning = false;
    }

    private void ApplyResponse(float scale, float opacity)
    {
        if (motionVisual is null || ambientVisual is null)
        {
            return;
        }

        motionVisual.Scale = new Vector3(scale, scale, 1);
        ambientVisual.Opacity = opacity;
    }

    private void StartArtworkOpacityAnimation(Visual visual, float opacity, int durationMs)
    {
        ScalarKeyFrameAnimation animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(1, opacity, easing);
        animation.Duration = TimeSpan.FromMilliseconds(durationMs);
        visual.StartAnimation(nameof(Visual.Opacity), animation);
    }

    private bool CanAnimate =>
        viewModel?.AmbientArtwork is ImageSource &&
        viewModel.HasSession &&
        viewModel.IsPlaying &&
        viewModel.ShowAudioVisualization;

    private bool ShouldPan =>
        viewModel?.AmbientArtwork is ImageSource &&
        viewModel.HasSession &&
        viewModel.ShowAudioVisualization;

    private static double Average(IReadOnlyList<double> levels, int start, int count)
    {
        int end = Math.Min(start + count, levels.Count);

        if (start >= end)
        {
            return 0;
        }

        double total = 0;

        for (int index = start; index < end; index++)
        {
            total += Math.Clamp(levels[index], 0, 1);
        }

        return total / (end - start);
    }
}
