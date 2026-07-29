using Microsoft.Graphics.Canvas.Effects;
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
    private MediaViewModel? viewModel;
    private Visual? ambientVisual;
    private Visual? motionVisual;
    private SpriteVisual? blurVisual;
    private CompositionVisualSurface? artworkSurface;
    private CompositionSurfaceBrush? artworkBrush;
    private CompositionEffectBrush? blurBrush;
    private CompositionRoundedRectangleGeometry? clipGeometry;
    private CompositionGeometricClip? roundedClip;
    private CompositionEasingFunction? easing;
    private ImplicitAnimationCollection? motionImplicitAnimations;
    private ImplicitAnimationCollection? ambientImplicitAnimations;
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
        clipGeometry = compositor.CreateRoundedRectangleGeometry();
        clipGeometry.CornerRadius = new Vector2(28);
        clipGeometry.Size = new Vector2((float)ActualWidth, (float)ActualHeight);
        roundedClip = compositor.CreateGeometricClip(clipGeometry);
        ambientVisual.Clip = roundedClip;
        motionVisual.CenterPoint = new Vector3((float)ActualWidth / 2, (float)ActualHeight / 2, 0);
        easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.22f, 0.72f), new Vector2(0.18f, 1));
        CreateBlurVisual(compositor);
        Subscribe();
        UpdateArtwork();
        UpdateState();
        ConfigureResponseAnimations(compositor);
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

        ElementCompositionPreview.SetElementChildVisual(BlurHost, null);
        motionImplicitAnimations?.Dispose();
        ambientImplicitAnimations?.Dispose();
        blurVisual?.Dispose();
        blurBrush?.Dispose();
        artworkBrush?.Dispose();
        artworkSurface?.Dispose();
        roundedClip?.Dispose();
        clipGeometry?.Dispose();
        blurVisual = null;
        blurBrush = null;
        artworkBrush = null;
        artworkSurface = null;
        roundedClip = null;
        clipGeometry = null;
        motionImplicitAnimations = null;
        ambientImplicitAnimations = null;
        ambientVisual = null;
        motionVisual = null;
        easing = null;
    }

    private void HandleSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (motionVisual is not null)
        {
            motionVisual.CenterPoint = new Vector3((float)args.NewSize.Width / 2, (float)args.NewSize.Height / 2, 0);
        }

        if (blurVisual is not null)
        {
            blurVisual.Size = new Vector2((float)args.NewSize.Width, (float)args.NewSize.Height);
        }

        if (artworkSurface is not null)
        {
            artworkSurface.SourceSize = new Vector2((float)args.NewSize.Width, (float)args.NewSize.Height);
        }

        if (clipGeometry is not null)
        {
            clipGeometry.Size = new Vector2((float)args.NewSize.Width, (float)args.NewSize.Height);
        }
    }

    private void CreateBlurVisual(Compositor compositor)
    {
        GaussianBlurEffect firstBlur = new()
        {
            BlurAmount = 250,
            BorderMode = EffectBorderMode.Hard,
            Source = new CompositionEffectSourceParameter("artwork")
        };
        GaussianBlurEffect secondBlur = new()
        {
            BlurAmount = 250,
            BorderMode = EffectBorderMode.Hard,
            Source = firstBlur
        };
        GaussianBlurEffect thirdBlur = new()
        {
            BlurAmount = 250,
            BorderMode = EffectBorderMode.Hard,
            Source = secondBlur
        };
        SaturationEffect saturation = new()
        {
            Saturation = 1.42f,
            Source = thirdBlur
        };

        artworkSurface = compositor.CreateVisualSurface();
        artworkSurface.SourceVisual = ElementCompositionPreview.GetElementVisual(AmbientArtwork);
        artworkSurface.SourceSize = new Vector2((float)ActualWidth, (float)ActualHeight);
        artworkBrush = compositor.CreateSurfaceBrush(artworkSurface);
        artworkBrush.Stretch = CompositionStretch.Fill;
        blurBrush = compositor.CreateEffectFactory(saturation).CreateBrush();
        blurBrush.SetSourceParameter("artwork", artworkBrush);

        blurVisual = compositor.CreateSpriteVisual();
        blurVisual.Brush = blurBrush;
        blurVisual.Opacity = 0.72f;
        blurVisual.Size = new Vector2((float)ActualWidth, (float)ActualHeight);
        ElementCompositionPreview.SetElementChildVisual(BlurHost, blurVisual);
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
        if (args.PropertyName == nameof(MediaViewModel.Artwork))
        {
            UpdateArtwork();
        }

        if (args.PropertyName is nameof(MediaViewModel.Artwork) or
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

        float scale = (float)(1.45 + (bass * 0.035));
        float opacity = (float)(0.72 + (energy * 0.04));

        ApplyResponse(scale, opacity);
    }

    private void UpdateArtwork() =>
        AmbientArtwork.Source = viewModel?.Artwork as ImageSource;

    private void UpdateState()
    {
        if (ambientVisual is null || motionVisual is null || easing is null)
        {
            return;
        }

        bool hasArtwork = viewModel?.HasSession == true && viewModel.Artwork is ImageSource;
        float opacity = hasArtwork ? 0.72f : 0;

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
            ApplyResponse(1.45f, opacity);
            return;
        }

        ApplyResponse(1.45f, opacity);
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
        opacityAnimation.Duration = TimeSpan.FromMilliseconds(220);
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
        viewModel?.Artwork is ImageSource &&
        viewModel.HasSession &&
        viewModel.IsPlaying &&
        viewModel.ShowAudioVisualization;

    private bool ShouldPan =>
        viewModel?.Artwork is ImageSource &&
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
