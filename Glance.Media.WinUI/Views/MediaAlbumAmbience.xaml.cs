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
    private double phase;

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
        UpdateState(animate: false);
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        Unsubscribe();
        ElementCompositionPreview.SetElementChildVisual(BlurHost, null);
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
        GaussianBlurEffect blur = new()
        {
            BlurAmount = 68,
            BorderMode = EffectBorderMode.Hard,
            Source = new CompositionEffectSourceParameter("artwork")
        };
        SaturationEffect saturation = new()
        {
            Saturation = 1.22f,
            Source = blur
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
        double upperRange = Average(args.Levels, 3, 2);
        phase += 0.018 + (energy * 0.035);

        float scale = (float)(1.16 + (bass * 0.04));
        float horizontalDrift = (float)((Math.Cos(phase) * (5.5 + (energy * 5.5))) + ((upperRange - bass) * 2));
        float verticalDrift = (float)(Math.Sin(phase * 0.72) * (3.5 + (energy * 4.5)));
        float opacity = (float)(0.36 + (energy * 0.06));

        AnimateMotion(scale, horizontalDrift, verticalDrift, opacity, TimeSpan.FromMilliseconds(160));
    }

    private void UpdateArtwork() =>
        AmbientArtwork.Source = viewModel?.Artwork as ImageSource;

    private void UpdateState(bool animate = true)
    {
        if (ambientVisual is null || motionVisual is null || easing is null)
        {
            return;
        }

        bool hasArtwork = viewModel?.HasSession == true && viewModel.Artwork is ImageSource;
        float opacity = hasArtwork ? 0.36f : 0;
        TimeSpan duration = animate ? TimeSpan.FromMilliseconds(220) : TimeSpan.Zero;

        if (!CanAnimate)
        {
            phase = 0;
            AnimateMotion(1.16f, 0, 0, opacity, duration);
            return;
        }

        AnimateMotion(1.16f, 0, 0, opacity, duration);
    }

    private void AnimateMotion(float scale, float horizontalDrift, float verticalDrift, float opacity, TimeSpan duration)
    {
        if (motionVisual is null || ambientVisual is null || easing is null)
        {
            return;
        }

        if (duration == TimeSpan.Zero)
        {
            motionVisual.StopAnimation(nameof(Visual.Scale));
            motionVisual.StopAnimation(nameof(Visual.Offset));
            ambientVisual.StopAnimation(nameof(Visual.Opacity));
            motionVisual.Scale = new Vector3(scale, scale, 1);
            motionVisual.Offset = new Vector3(horizontalDrift, verticalDrift, 0);
            ambientVisual.Opacity = opacity;
            return;
        }

        Compositor compositor = motionVisual.Compositor;
        Vector3KeyFrameAnimation scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.Duration = duration;
        scaleAnimation.InsertKeyFrame(1, new Vector3(scale, scale, 1), easing);
        motionVisual.StartAnimation(nameof(Visual.Scale), scaleAnimation);

        Vector3KeyFrameAnimation offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
        offsetAnimation.Duration = duration;
        offsetAnimation.InsertKeyFrame(1, new Vector3(horizontalDrift, verticalDrift, 0), easing);
        motionVisual.StartAnimation(nameof(Visual.Offset), offsetAnimation);

        ScalarKeyFrameAnimation opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.Duration = duration;
        opacityAnimation.InsertKeyFrame(1, opacity, easing);
        ambientVisual.StartAnimation(nameof(Visual.Opacity), opacityAnimation);
    }

    private bool CanAnimate =>
        viewModel?.Artwork is ImageSource &&
        viewModel.HasSession &&
        viewModel.IsPlaying &&
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
