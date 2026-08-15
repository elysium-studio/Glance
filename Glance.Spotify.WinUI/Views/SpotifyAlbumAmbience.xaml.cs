using Glance.Spotify;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Numerics;
using Windows.UI;

namespace Glance.Spotify.WinUI;

public sealed partial class SpotifyAlbumAmbience :
    UserControl
{
    private const int ArtworkTransitionDurationMs = 520;
    private Visual? ambientVisual;
    private Visual? motionVisual;
    private ContainerVisual? artworkContainerVisual;
    private SpriteVisual? currentArtworkVisual;
    private SpriteVisual? nextArtworkVisual;
    private CompositionSurfaceBrush? currentSurfaceBrush;
    private CompositionSurfaceBrush? nextSurfaceBrush;
    private CompositionEasingFunction? easing;
    private ImplicitAnimationCollection? motionImplicitAnimations;
    private ImplicitAnimationCollection? ambientImplicitAnimations;
    private SpotifyAmbientArtwork? desiredArtwork;
    private SpotifyAmbientArtwork? currentArtwork;
    private SpotifyAmbientArtwork? nextArtwork;
    private EventHandler<object>? artworkPreparationRenderingHandler;
    private int artworkTransitionGeneration;
    private bool isArtworkPreparing;
    private bool isArtworkTransitioning;
    private bool isPanning;

    public SpotifyAlbumAmbience() => InitializeComponent();

    internal event EventHandler? SurfaceAppearanceChanged;

    internal uint GetContrastingForeground(uint artworkColor)
    {
        Color artwork = FromArgb(artworkColor);
        Color surface = AcrylicOverlay.Fill switch
        {
            AcrylicBrush acrylic => EstimateAcrylicSurface(artwork, acrylic),
            SolidColorBrush solid => Blend(artwork,
                solid.Color,
                (solid.Color.A / 255d) * solid.Opacity),
            _ => artwork
        };
        return ToArgb(SpotifyAccentPalette.GetForeground(ToArgb(surface)));
    }

    public SpotifyViewModel? ViewModel
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
        motionVisual.CenterPoint = new Vector3((float)ActualWidth / 2, (float)ActualHeight / 2, 0);
        easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.22f, 0.72f), new Vector2(0.18f, 1));
        CreateArtworkVisual(compositor);
        Subscribe();
        UpdateArtwork();
        ConfigureResponseAnimations(compositor);
        UpdateState();
    }

    private void HandleActualThemeChanged(FrameworkElement sender, object args) => SurfaceAppearanceChanged?.Invoke(this, EventArgs.Empty);

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        Unsubscribe();
        StopPanning();
        artworkTransitionGeneration++;
        CancelArtworkPreparation();
        _ = motionVisual?.ImplicitAnimations = null;
        _ = ambientVisual?.ImplicitAnimations = null;
        ElementCompositionPreview.SetElementChildVisual(ArtworkHost, null);
        currentSurfaceBrush?.Dispose();
        nextSurfaceBrush?.Dispose();
        currentArtworkVisual?.Dispose();
        nextArtworkVisual?.Dispose();
        artworkContainerVisual?.Dispose();
        motionImplicitAnimations?.Dispose();
        ambientImplicitAnimations?.Dispose();
        nextArtwork?.Dispose();
        currentArtwork?.Dispose();
        currentSurfaceBrush = null;
        nextSurfaceBrush = null;
        currentArtworkVisual = null;
        nextArtworkVisual = null;
        artworkContainerVisual = null;
        currentArtwork = null;
        nextArtwork = null;
        desiredArtwork = null;
        motionImplicitAnimations = null;
        ambientImplicitAnimations = null;
        ambientVisual = null;
        motionVisual = null;
        easing = null;
        isArtworkPreparing = false;
        isArtworkTransitioning = false;
    }

    private void HandleSizeChanged(object sender, SizeChangedEventArgs args) => _ = motionVisual?.CenterPoint = new Vector3((float)args.NewSize.Width / 2, (float)args.NewSize.Height / 2, 0);

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
        if (!IsLoaded || ViewModel is null)
        {
            return;
        }

        ViewModel.PropertyChanged -= HandlePropertyChanged;
        ViewModel.PropertyChanged += HandlePropertyChanged;
    }

    private void Unsubscribe()
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= HandlePropertyChanged;
        }
    }

    private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SpotifyViewModel.AmbientArtwork))
        {
            UpdateArtwork();
        }

        if (args.PropertyName is nameof(SpotifyViewModel.AmbientArtwork) or
            nameof(SpotifyViewModel.HasPlayback))
        {
            UpdateState();
        }
    }

    private void UpdateArtwork()
    {
        SpotifyAmbientArtwork? artwork = ViewModel?.AmbientArtwork as SpotifyAmbientArtwork;
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

        if (isArtworkPreparing || isArtworkTransitioning)
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

    private void SetCurrentArtwork(SpotifyAmbientArtwork artwork)
    {
        currentArtwork = artwork.Retain();
        currentSurfaceBrush = CreateSurfaceBrush(artwork);
        currentArtworkVisual!.Brush = currentSurfaceBrush;
        currentArtworkVisual.Opacity = 1;
    }

    private void StartArtworkTransition(SpotifyAmbientArtwork artwork)
    {
        int transitionGeneration = ++artworkTransitionGeneration;
        nextArtwork = artwork.Retain();
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
        currentArtworkVisual.Opacity = 1;
        nextArtworkVisual.Opacity = 0;
        CompositionScopedBatch batch = currentArtworkVisual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        StartArtworkOpacityAnimation(currentArtworkVisual, 1, 0);
        StartArtworkOpacityAnimation(nextArtworkVisual, 0, 1);
        batch.Completed += (_, _) =>
        {
            batch.Dispose();

            if (transitionGeneration == artworkTransitionGeneration)
            {
                PromoteNextArtwork();

                if (desiredArtwork is not null && !ReferenceEquals(desiredArtwork, currentArtwork))
                {
                    StartArtworkTransition(desiredArtwork);
                }
            }
        };
        batch.End();
    }

    private void PromoteNextArtwork()
    {
        if (nextArtwork is null || nextSurfaceBrush is null ||
            currentArtworkVisual is null || nextArtworkVisual is null)
        {
            return;
        }

        currentArtworkVisual.StopAnimation(nameof(Visual.Opacity));
        nextArtworkVisual.StopAnimation(nameof(Visual.Opacity));
        SpotifyAmbientArtwork? previousArtwork = currentArtwork;
        CompositionSurfaceBrush? previousBrush = currentSurfaceBrush;
        currentArtwork = nextArtwork;
        currentSurfaceBrush = nextSurfaceBrush;
        nextArtwork = null;
        nextSurfaceBrush = null;
        (currentArtworkVisual, nextArtworkVisual) = (nextArtworkVisual, currentArtworkVisual);
        nextArtworkVisual.Brush = null;
        nextArtworkVisual.Opacity = 0;
        previousBrush?.Dispose();
        previousArtwork?.Dispose();
        isArtworkTransitioning = false;
    }

    private void CancelArtworkPreparation()
    {
        CancelArtworkPreparationHandler();
        isArtworkPreparing = false;
    }

    private void CancelArtworkPreparationHandler()
    {
        if (artworkPreparationRenderingHandler is not null)
        {
            CompositionTarget.Rendering -= artworkPreparationRenderingHandler;
            artworkPreparationRenderingHandler = null;
        }
    }

    private CompositionSurfaceBrush CreateSurfaceBrush(SpotifyAmbientArtwork artwork)
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

        bool hasArtwork = ViewModel?.HasPlayback == true &&
            ViewModel.AmbientArtwork is SpotifyAmbientArtwork;

        if (hasArtwork)
        {
            StartPanning();
        }
        else
        {
            StopPanning();
        }

        motionVisual.Scale = new Vector3(1.38f, 1.38f, 1);
        ambientVisual.Opacity = hasArtwork ? 0.92f : 0;
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

    private static Color EstimateAcrylicSurface(Color artwork, AcrylicBrush acrylic)
    {
        double luminosityOpacity = acrylic.TintLuminosityOpacity ?? 1;
        Color luminositySurface = Blend(artwork, acrylic.FallbackColor, luminosityOpacity);
        return Blend(luminositySurface, acrylic.TintColor, acrylic.TintOpacity * acrylic.Opacity);
    }

    private static Color Blend(Color background, Color foreground, double opacity)
    {
        double amount = Math.Clamp(opacity, 0, 1);
        return Color.FromArgb(255,
            BlendChannel(background.R, foreground.R, amount),
            BlendChannel(background.G, foreground.G, amount),
            BlendChannel(background.B, foreground.B, amount));
    }

    private static byte BlendChannel(byte background, byte foreground, double opacity) =>
        (byte)Math.Round(background + ((foreground - background) * opacity));

    private static Color FromArgb(uint value) => Color.FromArgb((byte)(value >> 24),
        (byte)(value >> 16),
        (byte)(value >> 8),
        (byte)value);

    private static uint ToArgb(Color color) => ((uint)color.A << 24) |
        ((uint)color.R << 16) |
        ((uint)color.G << 8) |
        color.B;
}
