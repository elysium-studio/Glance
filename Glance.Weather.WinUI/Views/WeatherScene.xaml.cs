using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;

namespace Glance.Weather.WinUI;

public sealed partial class WeatherScene :
    UserControl
{
    private readonly Random random = new();
    private WeatherViewModel? viewModel;
    private ContainerVisual? sceneVisual;
    private CompositionRoundedRectangleGeometry? clipGeometry;
    private CompositionGeometricClip? clip;
    private DispatcherQueueTimer? sizeTimer;

    public WeatherScene() => InitializeComponent();

    public WeatherViewModel? ViewModel
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
            RebuildScene();
        }
    }

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(this);
        clipGeometry = visual.Compositor.CreateRoundedRectangleGeometry();
        clipGeometry.CornerRadius = new Vector2(28);
        clipGeometry.Size = new Vector2((float)ActualWidth, (float)ActualHeight);
        clip = visual.Compositor.CreateGeometricClip(clipGeometry);
        visual.Clip = clip;
        sizeTimer = DispatcherQueue.CreateTimer();
        sizeTimer.Interval = TimeSpan.FromMilliseconds(80);
        sizeTimer.IsRepeating = false;
        sizeTimer.Tick += HandleSizeTimer;
        Subscribe();
        RebuildScene();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        Unsubscribe();
        if (sizeTimer is not null)
        {
            sizeTimer.Stop();
            sizeTimer.Tick -= HandleSizeTimer;
            sizeTimer = null;
        }

        ElementCompositionPreview.SetElementChildVisual(ParticleHost, null);
        sceneVisual?.Dispose();
        clip?.Dispose();
        clipGeometry?.Dispose();
        sceneVisual = null;
        clip = null;
        clipGeometry = null;
    }

    private void HandleSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (clipGeometry is not null)
        {
            clipGeometry.Size = new Vector2((float)args.NewSize.Width, (float)args.NewSize.Height);
        }

        sizeTimer?.Stop();
        sizeTimer?.Start();
    }

    private void HandleSizeTimer(DispatcherQueueTimer sender, object args) => RebuildScene();

    private void Subscribe()
    {
        if (!IsLoaded || viewModel is null)
        {
            return;
        }

        viewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        viewModel.PropertyChanged += HandleViewModelPropertyChanged;
    }

    private void Unsubscribe()
    {
        if (viewModel is not null)
        {
            viewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        }
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(WeatherViewModel.Scene) or nameof(WeatherViewModel.IsDay))
        {
            RebuildScene();
        }
    }

    private void RebuildScene()
    {
        if (!IsLoaded || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        ElementCompositionPreview.SetElementChildVisual(ParticleHost, null);
        sceneVisual?.Dispose();
        Compositor compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        sceneVisual = compositor.CreateContainerVisual();
        sceneVisual.RelativeSizeAdjustment = Vector2.One;
        ElementCompositionPreview.SetElementChildVisual(ParticleHost, sceneVisual);

        WeatherSceneKind scene = viewModel?.Scene ?? WeatherSceneKind.Unknown;
        bool isDay = viewModel?.IsDay ?? true;
        SceneRoot.Background = CreateBackground(scene, isDay);
        LightningBolt.Visibility = scene == WeatherSceneKind.Thunderstorm ? Visibility.Visible : Visibility.Collapsed;

        switch (scene)
        {
            case WeatherSceneKind.Clear:
                AddCelestial(compositor, false, isDay);
                break;
            case WeatherSceneKind.Hot:
                AddCelestial(compositor, true, true);
                AddHaze(compositor);
                break;
            case WeatherSceneKind.PartlyCloudy:
                AddCelestial(compositor, false, isDay);
                AddClouds(compositor, 3);
                break;
            case WeatherSceneKind.Cloudy:
                AddClouds(compositor, 6);
                break;
            case WeatherSceneKind.Rain:
                AddClouds(compositor, 3);
                AddRain(compositor, 28);
                break;
            case WeatherSceneKind.Snow:
                AddClouds(compositor, 3);
                AddSnow(compositor, 26);
                break;
            case WeatherSceneKind.Thunderstorm:
                AddClouds(compositor, 4);
                AddRain(compositor, 34);
                AddLightning(compositor);
                break;
            case WeatherSceneKind.Fog:
                AddFog(compositor);
                break;
        }
    }

    private Brush CreateBackground(WeatherSceneKind scene, bool isDay)
    {
        (Color start, Color end) = scene switch
        {
            WeatherSceneKind.Clear when isDay => (Color.FromArgb(255, 21, 112, 211), Color.FromArgb(255, 84, 187, 240)),
            WeatherSceneKind.Clear => (Color.FromArgb(255, 8, 20, 55), Color.FromArgb(255, 39, 58, 114)),
            WeatherSceneKind.Hot => (Color.FromArgb(255, 186, 65, 24), Color.FromArgb(255, 251, 146, 60)),
            WeatherSceneKind.PartlyCloudy => (Color.FromArgb(255, 39, 108, 162), Color.FromArgb(255, 113, 148, 175)),
            WeatherSceneKind.Cloudy => (Color.FromArgb(255, 42, 57, 72), Color.FromArgb(255, 92, 110, 126)),
            WeatherSceneKind.Rain => (Color.FromArgb(255, 16, 42, 67), Color.FromArgb(255, 45, 83, 112)),
            WeatherSceneKind.Snow => (Color.FromArgb(255, 48, 85, 115), Color.FromArgb(255, 153, 185, 207)),
            WeatherSceneKind.Thunderstorm => (Color.FromArgb(255, 21, 18, 45), Color.FromArgb(255, 51, 55, 88)),
            WeatherSceneKind.Fog => (Color.FromArgb(255, 55, 67, 78), Color.FromArgb(255, 126, 139, 148)),
            _ => (Color.FromArgb(255, 23, 43, 64), Color.FromArgb(255, 45, 67, 86))
        };

        return new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop { Color = start, Offset = 0 },
                new GradientStop { Color = end, Offset = 1 }
            }
        };
    }

    private void AddCelestial(Compositor compositor, bool hot, bool isDay)
    {
        float diameter = hot ? 100 : isDay ? 76 : 58;
        ContainerVisual sun = compositor.CreateContainerVisual();
        sun.Size = new Vector2(diameter);
        sun.Offset = new Vector3((float)ActualWidth - diameter * 0.65f, -diameter * 0.25f, 0);
        sun.CenterPoint = new Vector3(diameter / 2, diameter / 2, 0);

        for (int index = 0; index < 3; index++)
        {
            float inset = index * 12;
            Color color = isDay ?
                Color.FromArgb((byte)(45 + index * 40), 255, (byte)(hot ? 142 : 220), 73) :
                Color.FromArgb((byte)(45 + index * 40), 213, 229, 255);
            ShapeVisual disc = CreateDisc(compositor,
                diameter - inset * 2,
                color);
            disc.Offset = new Vector3(inset, inset, 0);
            sun.Children.InsertAtTop(disc);
        }

        Vector3KeyFrameAnimation scale = compositor.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(0, Vector3.One);
        scale.InsertKeyFrame(0.5f, new Vector3(1.08f));
        scale.InsertKeyFrame(1, Vector3.One);
        scale.Duration = TimeSpan.FromSeconds(4);
        scale.IterationBehavior = AnimationIterationBehavior.Forever;
        sun.StartAnimation("Scale", scale);
        sceneVisual?.Children.InsertAtTop(sun);
    }

    private void AddClouds(Compositor compositor, int count)
    {
        for (int index = 0; index < count; index++)
        {
            float width = RandomBetween(60, 105);
            float height = width * 0.4f;
            ContainerVisual cloud = compositor.CreateContainerVisual();
            cloud.Size = new Vector2(width, height);
            cloud.Offset = new Vector3(RandomBetween(-width, (float)ActualWidth), RandomBetween(4, (float)ActualHeight * 0.55f), 0);
            byte alpha = (byte)RandomBetween(35, 95);

            ShapeVisual left = CreateDisc(compositor, height * 0.8f, Color.FromArgb(alpha, 234, 242, 248));
            left.Offset = new Vector3(0, height * 0.2f, 0);
            ShapeVisual middle = CreateDisc(compositor, height, Color.FromArgb(alpha, 234, 242, 248));
            middle.Offset = new Vector3(width * 0.28f, 0, 0);
            ShapeVisual right = CreateDisc(compositor, height * 0.75f, Color.FromArgb(alpha, 234, 242, 248));
            right.Offset = new Vector3(width * 0.62f, height * 0.25f, 0);
            cloud.Children.InsertAtTop(left);
            cloud.Children.InsertAtTop(middle);
            cloud.Children.InsertAtTop(right);

            ScalarKeyFrameAnimation drift = compositor.CreateScalarKeyFrameAnimation();
            drift.InsertKeyFrame(0, -width);
            drift.InsertKeyFrame(1, (float)ActualWidth + width);
            drift.Duration = TimeSpan.FromSeconds(RandomBetween(12, 22));
            drift.IterationBehavior = AnimationIterationBehavior.Forever;
            cloud.StartAnimation("Offset.X", drift);
            sceneVisual?.Children.InsertAtTop(cloud);
        }
    }

    private void AddRain(Compositor compositor, int count)
    {
        for (int index = 0; index < count; index++)
        {
            SpriteVisual drop = compositor.CreateSpriteVisual();
            drop.Brush = compositor.CreateColorBrush(Color.FromArgb((byte)RandomBetween(75, 160), 158, 214, 255));
            drop.Size = new Vector2(1.2f, RandomBetween(8, 15));
            drop.Offset = new Vector3(RandomBetween(0, (float)ActualWidth + 50), RandomBetween(-(float)ActualHeight, 0), 0);
            ScalarKeyFrameAnimation fall = compositor.CreateScalarKeyFrameAnimation();
            fall.InsertKeyFrame(0, -(float)ActualHeight * 0.2f);
            fall.InsertKeyFrame(1, (float)ActualHeight + 20);
            fall.Duration = TimeSpan.FromSeconds(RandomBetween(0.65f, 1.15f));
            fall.DelayTime = TimeSpan.FromSeconds(RandomBetween(0, 1));
            fall.IterationBehavior = AnimationIterationBehavior.Forever;
            drop.StartAnimation("Offset.Y", fall);
            sceneVisual?.Children.InsertAtTop(drop);
        }
    }

    private void AddSnow(Compositor compositor, int count)
    {
        for (int index = 0; index < count; index++)
        {
            float size = RandomBetween(2.5f, 6);
            ShapeVisual flake = CreateDisc(compositor, size, Color.FromArgb((byte)RandomBetween(100, 220), 248, 252, 255));
            flake.Offset = new Vector3(RandomBetween(0, (float)ActualWidth), RandomBetween(-(float)ActualHeight, 0), 0);
            ScalarKeyFrameAnimation fall = compositor.CreateScalarKeyFrameAnimation();
            fall.InsertKeyFrame(0, -(float)ActualHeight * 0.15f);
            fall.InsertKeyFrame(1, (float)ActualHeight + 10);
            fall.Duration = TimeSpan.FromSeconds(RandomBetween(3, 6));
            fall.DelayTime = TimeSpan.FromSeconds(RandomBetween(0, 2));
            fall.IterationBehavior = AnimationIterationBehavior.Forever;
            flake.StartAnimation("Offset.Y", fall);
            sceneVisual?.Children.InsertAtTop(flake);
        }
    }

    private void AddLightning(Compositor compositor)
    {
        SpriteVisual flash = compositor.CreateSpriteVisual();
        flash.RelativeSizeAdjustment = Vector2.One;
        flash.Brush = compositor.CreateColorBrush(Color.FromArgb(255, 216, 219, 255));
        flash.Opacity = 0;
        ScalarKeyFrameAnimation pulse = compositor.CreateScalarKeyFrameAnimation();
        pulse.InsertKeyFrame(0, 0);
        pulse.InsertKeyFrame(0.68f, 0);
        pulse.InsertKeyFrame(0.7f, 0.7f);
        pulse.InsertKeyFrame(0.73f, 0);
        pulse.InsertKeyFrame(0.76f, 0.4f);
        pulse.InsertKeyFrame(0.8f, 0);
        pulse.InsertKeyFrame(1, 0);
        pulse.Duration = TimeSpan.FromSeconds(4.5);
        pulse.IterationBehavior = AnimationIterationBehavior.Forever;
        flash.StartAnimation("Opacity", pulse);
        sceneVisual?.Children.InsertAtTop(flash);

        Visual bolt = ElementCompositionPreview.GetElementVisual(LightningBolt);
        ScalarKeyFrameAnimation boltPulse = compositor.CreateScalarKeyFrameAnimation();
        boltPulse.InsertKeyFrame(0, 0);
        boltPulse.InsertKeyFrame(0.68f, 0);
        boltPulse.InsertKeyFrame(0.7f, 1);
        boltPulse.InsertKeyFrame(0.73f, 0);
        boltPulse.InsertKeyFrame(0.76f, 0.85f);
        boltPulse.InsertKeyFrame(0.8f, 0);
        boltPulse.InsertKeyFrame(1, 0);
        boltPulse.Duration = TimeSpan.FromSeconds(4.5);
        boltPulse.IterationBehavior = AnimationIterationBehavior.Forever;
        bolt.StartAnimation("Opacity", boltPulse);
    }

    private void AddFog(Compositor compositor)
    {
        for (int index = 0; index < 7; index++)
        {
            SpriteVisual band = compositor.CreateSpriteVisual();
            band.Brush = compositor.CreateColorBrush(Color.FromArgb((byte)(30 + index * 7), 230, 238, 243));
            band.Size = new Vector2((float)ActualWidth * RandomBetween(0.45f, 0.85f), 2);
            band.Offset = new Vector3(RandomBetween(-30, 30), 12 + index * 14, 0);
            ScalarKeyFrameAnimation drift = compositor.CreateScalarKeyFrameAnimation();
            drift.InsertKeyFrame(0, -24);
            drift.InsertKeyFrame(0.5f, 24);
            drift.InsertKeyFrame(1, -24);
            drift.Duration = TimeSpan.FromSeconds(RandomBetween(5, 9));
            drift.IterationBehavior = AnimationIterationBehavior.Forever;
            band.StartAnimation("Offset.X", drift);
            sceneVisual?.Children.InsertAtTop(band);
        }
    }

    private void AddHaze(Compositor compositor)
    {
        for (int index = 0; index < 5; index++)
        {
            SpriteVisual haze = compositor.CreateSpriteVisual();
            haze.Brush = compositor.CreateColorBrush(Color.FromArgb(35, 255, 228, 150));
            haze.Size = new Vector2((float)ActualWidth * 0.65f, 1.5f);
            haze.Offset = new Vector3((float)ActualWidth * 0.15f, 34 + index * 13, 0);
            ScalarKeyFrameAnimation shimmer = compositor.CreateScalarKeyFrameAnimation();
            shimmer.InsertKeyFrame(0, 0.15f);
            shimmer.InsertKeyFrame(0.5f, 0.7f);
            shimmer.InsertKeyFrame(1, 0.15f);
            shimmer.Duration = TimeSpan.FromSeconds(2 + index * 0.3);
            shimmer.IterationBehavior = AnimationIterationBehavior.Forever;
            haze.StartAnimation("Opacity", shimmer);
            sceneVisual?.Children.InsertAtTop(haze);
        }
    }

    private static ShapeVisual CreateDisc(Compositor compositor, float diameter, Color color)
    {
        ShapeVisual visual = compositor.CreateShapeVisual();
        visual.Size = new Vector2(diameter);
        CompositionEllipseGeometry geometry = compositor.CreateEllipseGeometry();
        geometry.Center = new Vector2(diameter / 2);
        geometry.Radius = new Vector2(diameter / 2);
        CompositionSpriteShape shape = compositor.CreateSpriteShape(geometry);
        shape.FillBrush = compositor.CreateColorBrush(color);
        visual.Shapes.Add(shape);
        return visual;
    }

    private float RandomBetween(float minimum, float maximum) =>
        minimum + (float)random.NextDouble() * (maximum - minimum);
}
