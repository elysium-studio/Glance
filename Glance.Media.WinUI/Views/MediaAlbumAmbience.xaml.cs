using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Threading;

namespace Glance.Media.WinUI;

public sealed partial class MediaAlbumAmbience :
    UserControl
{
    private const int ArtworkTransitionDurationMs = 520;

    private static int nextDiagnosticId;

    private readonly int diagnosticId = Interlocked.Increment(ref nextDiagnosticId);
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
    private EventHandler<object>? artworkPreparationRenderingHandler;
    private int artworkTransitionGeneration;
    private bool isArtworkPreparing;
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
        MediaTransitionDiagnostics.Write(DiagnosticSource, $"Loaded Size={ActualWidth:F0}x{ActualHeight:F0} Desired={(viewModel?.AmbientArtwork as MediaAmbientArtwork)?.Id.ToString() ?? "null"}");
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
        MediaTransitionDiagnostics.Write(DiagnosticSource, $"Unloaded Current={currentArtwork?.Id.ToString() ?? "null"} Next={nextArtwork?.Id.ToString() ?? "null"} Desired={desiredArtwork?.Id.ToString() ?? "null"}");
        Unsubscribe();
        StopPanning();
        artworkTransitionGeneration++;
        CancelArtworkPreparation();

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
        MediaTransitionDiagnostics.Write(DiagnosticSource, $"Update requested Desired={artwork?.Id.ToString() ?? "null"} Current={currentArtwork?.Id.ToString() ?? "null"} Next={nextArtwork?.Id.ToString() ?? "null"} Preparing={isArtworkPreparing} Transitioning={isArtworkTransitioning} Loaded={IsLoaded}");

        if (currentArtworkVisual is null || nextArtworkVisual is null)
        {
            MediaTransitionDiagnostics.Write(DiagnosticSource, "Update deferred because visuals are unavailable");
            return;
        }

        if (artwork is null ||
            ReferenceEquals(currentArtwork, artwork) ||
            ReferenceEquals(nextArtwork, artwork))
        {
            MediaTransitionDiagnostics.Write(DiagnosticSource, "Update ignored because artwork is null or already staged");
            return;
        }

        if (isArtworkPreparing || isArtworkTransitioning)
        {
            MediaTransitionDiagnostics.Write(DiagnosticSource, "Update queued as desired artwork during active transition");
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
        MediaTransitionDiagnostics.Write(DiagnosticSource, $"Set current Artwork={artwork.Id}");
        currentArtwork = artwork;
        currentSurfaceBrush = CreateSurfaceBrush(artwork);
        currentArtworkVisual!.Brush = currentSurfaceBrush;
        currentArtworkVisual.Opacity = 1;
    }

    private void StartArtworkTransition(MediaAmbientArtwork artwork)
    {
        int transitionGeneration = ++artworkTransitionGeneration;
        MediaTransitionDiagnostics.Write(DiagnosticSource, $"Prepare transition Generation={transitionGeneration} From={currentArtwork?.Id.ToString() ?? "null"} To={artwork.Id}");
        nextArtwork = artwork;
        nextSurfaceBrush = CreateSurfaceBrush(artwork);
        nextArtworkVisual!.Brush = nextSurfaceBrush;
        currentArtworkVisual!.StopAnimation(nameof(Visual.Opacity));
        nextArtworkVisual.StopAnimation(nameof(Visual.Opacity));
        currentArtworkVisual.Opacity = 1;
        nextArtworkVisual.Opacity = 0;
        artworkContainerVisual!.Children.Remove(nextArtworkVisual);
        artworkContainerVisual.Children.InsertAtTop(nextArtworkVisual);
        isArtworkPreparing = true;
        artworkPreparationRenderingHandler = (_, _) =>
        {
            CancelArtworkPreparationHandler();

            if (transitionGeneration == artworkTransitionGeneration &&
                ReferenceEquals(nextArtwork, artwork))
            {
                MediaTransitionDiagnostics.Write(DiagnosticSource, $"Preparation frame ready Generation={transitionGeneration} To={artwork.Id}");
                BeginArtworkTransition(transitionGeneration);
            }
            else
            {
                MediaTransitionDiagnostics.Write(DiagnosticSource, $"Preparation superseded Generation={transitionGeneration} CurrentGeneration={artworkTransitionGeneration}");
            }
        };
        CompositionTarget.Rendering += artworkPreparationRenderingHandler;
    }

    private void BeginArtworkTransition(int transitionGeneration)
    {
        if (!isArtworkPreparing ||
            currentArtworkVisual is null ||
            nextArtworkVisual is null)
        {
            MediaTransitionDiagnostics.Write(DiagnosticSource, $"Transition rejected Generation={transitionGeneration} Preparing={isArtworkPreparing}");
            return;
        }

        MediaTransitionDiagnostics.Write(DiagnosticSource, $"Transition begin Generation={transitionGeneration} From={currentArtwork?.Id.ToString() ?? "null"} To={nextArtwork?.Id.ToString() ?? "null"}");
        isArtworkPreparing = false;
        isArtworkTransitioning = true;
        currentArtworkVisual.Opacity = 0;
        nextArtworkVisual.Opacity = 1;
        CompositionScopedBatch batch = currentArtworkVisual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        StartArtworkOpacityAnimation(currentArtworkVisual, 1, 0);
        StartArtworkOpacityAnimation(nextArtworkVisual, 0, 1);
        batch.Completed += (_, _) =>
        {
            batch.Dispose();

            if (transitionGeneration == artworkTransitionGeneration)
            {
                MediaTransitionDiagnostics.Write(DiagnosticSource, $"Transition completed Generation={transitionGeneration}");
                PromoteNextArtwork();

                if (desiredArtwork is not null &&
                    !ReferenceEquals(desiredArtwork, currentArtwork))
                {
                    StartArtworkTransition(desiredArtwork);
                }
            }
            else
            {
                MediaTransitionDiagnostics.Write(DiagnosticSource, $"Transition completion superseded Generation={transitionGeneration} CurrentGeneration={artworkTransitionGeneration}");
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
            MediaTransitionDiagnostics.Write(DiagnosticSource, "Promotion rejected because transition state is incomplete");
            return;
        }

        MediaTransitionDiagnostics.Write(DiagnosticSource, $"Promoting From={currentArtwork?.Id.ToString() ?? "null"} To={nextArtwork.Id}");
        currentArtworkVisual.StopAnimation(nameof(Visual.Opacity));
        nextArtworkVisual.StopAnimation(nameof(Visual.Opacity));
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
            MediaTransitionDiagnostics.Write(DiagnosticSource, $"Disposing previous Artwork={previousArtwork.Id}");
            previousArtwork.Dispose();
        }

        isArtworkTransitioning = false;
        MediaTransitionDiagnostics.Write(DiagnosticSource, $"Promotion complete Current={currentArtwork?.Id.ToString() ?? "null"}");
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

    private CompositionSurfaceBrush CreateSurfaceBrush(MediaAmbientArtwork artwork)
    {
        CompositionSurfaceBrush brush = currentArtworkVisual!.Compositor.CreateSurfaceBrush(artwork.Surface);
        brush.Stretch = CompositionStretch.UniformToFill;
        brush.HorizontalAlignmentRatio = 0.5f;
        brush.VerticalAlignmentRatio = 0.5f;
        return brush;
    }

    private void StartArtworkOpacityAnimation(Visual visual, float from, float to)
    {
        ScalarKeyFrameAnimation animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, easing);
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

    private string DiagnosticSource => $"Ambience#{diagnosticId}[{ActualWidth:F0}x{ActualHeight:F0}]";
}
