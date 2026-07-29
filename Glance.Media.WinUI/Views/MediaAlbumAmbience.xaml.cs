using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;

namespace Glance.Media.WinUI;

public sealed partial class MediaAlbumAmbience :
    UserControl
{
    private const int ArtworkTransitionDurationMs = 520;

    private MediaViewModel? viewModel;
    private Visual? ambientVisual;
    private Visual? motionVisual;
    private ContainerVisual? artworkContainerVisual;
    private SpriteVisual? currentArtworkVisual;
    private SpriteVisual? nextArtworkVisual;
    private CompositionSurfaceBrush? currentSurfaceBrush;
    private CompositionSurfaceBrush? nextSurfaceBrush;
    private CompositionRoundedRectangleGeometry? clipGeometry;
    private CompositionGeometricClip? roundedClip;
    private CompositionEasingFunction? easing;
    private ImplicitAnimationCollection? motionImplicitAnimations;
    private ImplicitAnimationCollection? ambientImplicitAnimations;
    private MediaAmbientArtwork? desiredArtwork;
    private MediaAmbientArtwork? currentArtwork;
    private MediaAmbientArtwork? nextArtwork;
    private int artworkTransitionGeneration;
    private bool isArtworkTransitioning;
    private bool isPanning;

    public MediaAlbumAmbience() => InitializeComponent();

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
        ambientVisual.Opacity = 0;
        clipGeometry = compositor.CreateRoundedRectangleGeometry();
        clipGeometry.CornerRadius = new Vector2(28);
        clipGeometry.Size = new Vector2((float)ActualWidth, (float)ActualHeight);
        roundedClip = compositor.CreateGeometricClip(clipGeometry);
        ambientVisual.Clip = roundedClip;
        motionVisual.CenterPoint = new Vector3((float)ActualWidth / 2, (float)ActualHeight / 2, 0);
        easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.22f, 0.72f), new Vector2(0.18f, 1));
        CreateArtworkVisual(compositor);
        Subscribe();
        UpdateArtwork();
        ConfigureResponseAnimations(compositor);
        UpdateState();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        Unsubscribe();
        StopPanning();
        artworkTransitionGeneration++;

        if (motionVisual is not null)
        {
            motionVisual.ImplicitAnimations = null;
        }

        if (ambientVisual is not null)
        {
            ambientVisual.ImplicitAnimations = null;
        }

        if (nextArtwork is not null &&
            !ReferenceEquals(nextArtwork, viewModel?.AmbientArtwork))
        {
            nextArtwork.Dispose();
        }

        if (currentArtwork is not null &&
            !ReferenceEquals(currentArtwork, viewModel?.AmbientArtwork))
        {
            currentArtwork.Dispose();
        }

        ElementCompositionPreview.SetElementChildVisual(ArtworkHost, null);
        currentSurfaceBrush?.Dispose();
        nextSurfaceBrush?.Dispose();
        currentArtworkVisual?.Dispose();
        nextArtworkVisual?.Dispose();
        artworkContainerVisual?.Dispose();
        motionImplicitAnimations?.Dispose();
        ambientImplicitAnimations?.Dispose();
        roundedClip?.Dispose();
        clipGeometry?.Dispose();
        currentSurfaceBrush = null;
        nextSurfaceBrush = null;
        currentArtworkVisual = null;
        nextArtworkVisual = null;
        artworkContainerVisual = null;
        currentArtwork = null;
        nextArtwork = null;
        desiredArtwork = null;
        roundedClip = null;
        clipGeometry = null;
        motionImplicitAnimations = null;
        ambientImplicitAnimations = null;
        ambientVisual = null;
        motionVisual = null;
        easing = null;
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

    private void CreateArtworkVisual(Compositor compositor)
    {
        artworkContainerVisual = compositor.CreateContainerVisual();
        artworkContainerVisual.RelativeSizeAdjustment = Vector2.One;
        currentArtworkVisual = compositor.CreateSpriteVisual();
        currentArtworkVisual.RelativeSizeAdjustment = Vector2.One;
        currentArtworkVisual.Opacity = 0;
        nextArtworkVisual = compositor.CreateSpriteVisual();
        nextArtworkVisual.RelativeSizeAdjustment = Vector2.One;
        nextArtworkVisual.Opacity = 0;
        artworkContainerVisual.Children.InsertAtBottom(currentArtworkVisual);
        artworkContainerVisual.Children.InsertAtTop(nextArtworkVisual);
        ElementCompositionPreview.SetElementChildVisual(ArtworkHost, artworkContainerVisual);
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
        MediaAmbientArtwork? artwork = viewModel?.AmbientArtwork as MediaAmbientArtwork;
        desiredArtwork = artwork;

        if (currentArtworkVisual is null || nextArtworkVisual is null)
        {
            return;
        }

        if (artwork is null ||
            ReferenceEquals(currentArtwork, artwork) ||
            ReferenceEquals(nextArtwork, artwork))
        {
            return;
        }

        if (isArtworkTransitioning)
        {
            return;
        }

        if (currentArtwork is null)
        {
            SetCurrentArtwork(artwork);
            return;
        }

        StartArtworkTransition(artwork);
    }

    private void SetCurrentArtwork(MediaAmbientArtwork artwork)
    {
        currentArtwork = artwork;
        currentSurfaceBrush = CreateSurfaceBrush(artwork);
        currentArtworkVisual!.Brush = currentSurfaceBrush;
        currentArtworkVisual.Opacity = 1;
    }

    private void StartArtworkTransition(MediaAmbientArtwork artwork)
    {
        int transitionGeneration = ++artworkTransitionGeneration;
        nextArtwork = artwork;
        nextSurfaceBrush = CreateSurfaceBrush(artwork);
        nextArtworkVisual!.Brush = nextSurfaceBrush;
        currentArtworkVisual!.StopAnimation(nameof(Visual.Opacity));
        nextArtworkVisual.StopAnimation(nameof(Visual.Opacity));
        currentArtworkVisual.Opacity = 1;
        nextArtworkVisual.Opacity = 0;
        artworkContainerVisual!.Children.Remove(nextArtworkVisual);
        artworkContainerVisual.Children.InsertAtTop(nextArtworkVisual);
        isArtworkTransitioning = true;

        CompositionScopedBatch batch = currentArtworkVisual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        StartArtworkOpacityAnimation(currentArtworkVisual, 0);
        StartArtworkOpacityAnimation(nextArtworkVisual, 1);
        batch.Completed += (_, _) =>
        {
            batch.Dispose();

            if (transitionGeneration == artworkTransitionGeneration)
            {
                PromoteNextArtwork();

                if (desiredArtwork is not null &&
                    !ReferenceEquals(desiredArtwork, currentArtwork))
                {
                    StartArtworkTransition(desiredArtwork);
                }
            }
        };
        batch.End();
    }

    private void PromoteNextArtwork()
    {
        if (nextArtwork is null ||
            nextSurfaceBrush is null ||
            currentArtworkVisual is null ||
            nextArtworkVisual is null)
        {
            return;
        }

        currentArtworkVisual.StopAnimation(nameof(Visual.Opacity));
        nextArtworkVisual.StopAnimation(nameof(Visual.Opacity));
        currentArtworkVisual.Opacity = 0;
        nextArtworkVisual.Opacity = 1;
        MediaAmbientArtwork? previousArtwork = currentArtwork;
        CompositionSurfaceBrush? previousBrush = currentSurfaceBrush;
        currentArtwork = nextArtwork;
        currentSurfaceBrush = nextSurfaceBrush;
        nextArtwork = null;
        nextSurfaceBrush = null;
        (currentArtworkVisual, nextArtworkVisual) = (nextArtworkVisual, currentArtworkVisual);
        nextArtworkVisual.Brush = null;
        nextArtworkVisual.Opacity = 0;
        previousBrush?.Dispose();

        if (previousArtwork is not null &&
            !ReferenceEquals(previousArtwork, desiredArtwork) &&
            !ReferenceEquals(previousArtwork, viewModel?.AmbientArtwork))
        {
            previousArtwork.Dispose();
        }

        isArtworkTransitioning = false;
    }

    private CompositionSurfaceBrush CreateSurfaceBrush(MediaAmbientArtwork artwork)
    {
        CompositionSurfaceBrush brush = currentArtworkVisual!.Compositor.CreateSurfaceBrush(artwork.Surface);
        brush.Stretch = CompositionStretch.UniformToFill;
        brush.HorizontalAlignmentRatio = 0.5f;
        brush.VerticalAlignmentRatio = 0.5f;
        return brush;
    }

    private void StartArtworkOpacityAnimation(Visual visual, float opacity)
    {
        ScalarKeyFrameAnimation animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(1, opacity, easing);
        animation.Duration = TimeSpan.FromMilliseconds(ArtworkTransitionDurationMs);
        visual.StartAnimation(nameof(Visual.Opacity), animation);
    }

    private void UpdateState()
    {
        if (ambientVisual is null || motionVisual is null || easing is null)
        {
            return;
        }

        bool hasArtwork = viewModel?.HasSession == true &&
            viewModel.AmbientArtwork is MediaAmbientArtwork;
        float opacity = hasArtwork ? 0.92f : 0;

        if (ShouldPan)
        {
            StartPanning();
        }
        else
        {
            StopPanning();
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

    private bool CanAnimate =>
        viewModel?.AmbientArtwork is MediaAmbientArtwork &&
        viewModel.HasSession &&
        viewModel.IsPlaying &&
        viewModel.ShowAudioVisualization;

    private bool ShouldPan =>
        viewModel?.AmbientArtwork is MediaAmbientArtwork &&
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
