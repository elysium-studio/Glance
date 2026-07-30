using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
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
    private ContainerVisual? activeSceneLayer;
    private Canvas? activeCloudLayer;
    private FrameworkElement? activeBackgroundLayer;
    private CompositionRoundedRectangleGeometry? clipGeometry;
    private CompositionGeometricClip? clip;
    private DispatcherQueueTimer? lightningTimer;
    private SpriteVisual? lightningFlash;
    private WeatherSceneState? activeState;
    private bool rebuildAfterTransition;
    private bool rebuildQueued;
    private bool transitionActive;

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
        Subscribe();
        QueueSceneRebuild();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        Unsubscribe();
        ElementCompositionPreview.SetElementChildVisual(ParticleHost, null);
        BackgroundHost.Children.Clear();
        CloudLayerHost.Children.Clear();
        StopLightning();
        sceneVisual?.Dispose();
        clip?.Dispose();
        clipGeometry?.Dispose();
        sceneVisual = null;
        activeSceneLayer = null;
        activeCloudLayer = null;
        activeBackgroundLayer = null;
        activeState = null;
        rebuildAfterTransition = false;
        transitionActive = false;
        clip = null;
        clipGeometry = null;
    }

    private void HandleSizeChanged(object sender, SizeChangedEventArgs args)
    {
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
        if (args.PropertyName is nameof(WeatherViewModel.WeatherTime) or
            nameof(WeatherViewModel.WeatherSky) or
            nameof(WeatherViewModel.WeatherCelestial) or
            nameof(WeatherViewModel.WeatherEffect) or
            nameof(WeatherViewModel.WeatherTemperature))
        {
            QueueSceneRebuild();
        }
    }

    private void QueueSceneRebuild()
    {
        if (rebuildQueued)
        {
            return;
        }

        rebuildQueued = true;
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            rebuildQueued = false;
            RebuildScene();
        });
    }

    private void RebuildScene()
    {
        if (!IsLoaded || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        Compositor compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;

        if (sceneVisual is null)
        {
            sceneVisual = compositor.CreateContainerVisual();
            sceneVisual.RelativeSizeAdjustment = Vector2.One;
            ElementCompositionPreview.SetElementChildVisual(ParticleHost, sceneVisual);
        }

        WeatherTimeOfDay time = viewModel?.WeatherTime ?? WeatherTimeOfDay.Afternoon;
        WeatherSky sky = viewModel?.WeatherSky ?? WeatherSky.Clear;
        WeatherCelestial celestial = viewModel?.WeatherCelestial ?? WeatherCelestial.Sun;
        WeatherEffect effect = viewModel?.WeatherEffect ?? WeatherEffect.None;
        WeatherTemperature temperature = viewModel?.WeatherTemperature ?? WeatherTemperature.Normal;
        WeatherSceneState state = new(time, sky, celestial, effect, temperature);

        if (transitionActive)
        {
            rebuildAfterTransition = true;
            return;
        }

        if (activeState == state)
        {
            return;
        }

        StopLightning();
        ContainerVisual? previousSceneLayer = activeSceneLayer;
        Canvas? previousCloudLayer = activeCloudLayer;
        FrameworkElement? previousBackgroundLayer = activeBackgroundLayer;
        activeSceneLayer = compositor.CreateContainerVisual();
        activeSceneLayer.RelativeSizeAdjustment = Vector2.One;
        activeSceneLayer.Opacity = previousSceneLayer is null ? 1 : 0;
        sceneVisual.Children.InsertAtTop(activeSceneLayer);
        activeCloudLayer = new Canvas { Opacity = previousCloudLayer is null ? 1 : 0, IsHitTestVisible = false };
        CloudLayerHost.Children.Add(activeCloudLayer);
        activeBackgroundLayer = new Border
        {
            Background = CreateBackground(time, sky, effect, temperature),
            Opacity = previousBackgroundLayer is null ? 1 : 0,
            IsHitTestVisible = false
        };
        BackgroundHost.Children.Add(activeBackgroundLayer);
        activeState = state;

        if (celestial != WeatherCelestial.None)
        {
            AddCelestial(compositor, temperature == WeatherTemperature.Hot, celestial, time);
        }

        if (time == WeatherTimeOfDay.Night && sky != WeatherSky.Cloudy)
        {
            AddStars(compositor);
        }

        if (sky == WeatherSky.PartlyCloudy)
        {
            AddClouds(compositor, 2);
        }
        else if (sky == WeatherSky.Cloudy)
        {
            AddClouds(compositor, 3);
        }

        if (temperature == WeatherTemperature.Hot)
        {
            AddHaze(compositor);
        }

        switch (effect)
        {
            case WeatherEffect.Rain:
                AddRain(compositor, 28);
                break;
            case WeatherEffect.Snow:
                AddSnow(compositor, 26);
                break;
            case WeatherEffect.Thunderstorm:
                AddRain(compositor, 34);
                AddLightning(compositor);
                break;
            case WeatherEffect.Fog:
                AddFog(compositor);
                break;
        }

        if (previousSceneLayer is not null && previousCloudLayer is not null && previousBackgroundLayer is not null)
        {
            CrossFadeScene(compositor,
                previousSceneLayer,
                activeSceneLayer,
                previousCloudLayer,
                activeCloudLayer,
                previousBackgroundLayer,
                activeBackgroundLayer);
        }
    }

    private void CrossFadeScene(Compositor compositor,
        ContainerVisual previousScene,
        ContainerVisual nextScene,
        Canvas previousClouds,
        Canvas nextClouds,
        FrameworkElement previousBackground,
        FrameworkElement nextBackground)
    {
        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.22f, 1), new Vector2(0.36f, 1));
        transitionActive = true;
        CompositionScopedBatch batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        StartOpacityTransition(compositor, previousScene, 1, 0, easing);
        StartOpacityTransition(compositor, nextScene, 0, 1, easing);
        StartOpacityTransition(compositor, ElementCompositionPreview.GetElementVisual(previousClouds), 1, 0, easing);
        StartOpacityTransition(compositor, ElementCompositionPreview.GetElementVisual(nextClouds), 0, 1, easing);
        StartOpacityTransition(compositor, ElementCompositionPreview.GetElementVisual(previousBackground), 1, 0, easing);
        StartOpacityTransition(compositor, ElementCompositionPreview.GetElementVisual(nextBackground), 0, 1, easing);
        batch.End();
        batch.Completed += (sender, args) => DispatcherQueue.TryEnqueue(() =>
        {
            nextScene.StopAnimation("Opacity");
            nextScene.Opacity = 1;
            Visual nextCloudVisual = ElementCompositionPreview.GetElementVisual(nextClouds);
            nextCloudVisual.StopAnimation("Opacity");
            nextCloudVisual.Opacity = 1;
            nextClouds.Opacity = 1;
            Visual nextBackgroundVisual = ElementCompositionPreview.GetElementVisual(nextBackground);
            nextBackgroundVisual.StopAnimation("Opacity");
            nextBackgroundVisual.Opacity = 1;
            nextBackground.Opacity = 1;
            sceneVisual?.Children.Remove(previousScene);
            previousScene.Dispose();
            CloudLayerHost.Children.Remove(previousClouds);
            BackgroundHost.Children.Remove(previousBackground);
            batch.Dispose();
            transitionActive = false;

            if (rebuildAfterTransition && IsLoaded)
            {
                rebuildAfterTransition = false;
                QueueSceneRebuild();
            }
        });
    }

    private static void StartOpacityTransition(Compositor compositor, Visual visual, float from, float to, CompositionEasingFunction easing)
    {
        ScalarKeyFrameAnimation animation = compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, easing);
        animation.Duration = TimeSpan.FromMilliseconds(420);
        visual.StartAnimation("Opacity", animation);
    }

    private Brush CreateBackground(WeatherTimeOfDay time, WeatherSky sky, WeatherEffect effect, WeatherTemperature temperature)
    {
        (Color start, Color end) = time switch
        {
            WeatherTimeOfDay.Dawn => (Color.FromArgb(255, 65, 72, 139), Color.FromArgb(255, 242, 151, 124)),
            WeatherTimeOfDay.Morning => (Color.FromArgb(255, 48, 139, 214), Color.FromArgb(255, 150, 211, 239)),
            WeatherTimeOfDay.Afternoon => (Color.FromArgb(255, 20, 105, 202), Color.FromArgb(255, 83, 182, 235)),
            WeatherTimeOfDay.Evening => (Color.FromArgb(255, 31, 112, 190), Color.FromArgb(255, 245, 181, 105)),
            WeatherTimeOfDay.Dusk => (Color.FromArgb(255, 50, 54, 112), Color.FromArgb(255, 204, 95, 133)),
            WeatherTimeOfDay.Night => (Color.FromArgb(255, 7, 18, 50), Color.FromArgb(255, 32, 50, 101)),
            _ => (Color.FromArgb(255, 20, 105, 202), Color.FromArgb(255, 83, 182, 235))
        };

        if (temperature == WeatherTemperature.Hot && time is not WeatherTimeOfDay.Night)
        {
            start = Blend(start, Color.FromArgb(255, 184, 65, 26), 0.44f);
            end = Blend(end, Color.FromArgb(255, 250, 153, 61), 0.44f);
        }

        if (sky == WeatherSky.Cloudy)
        {
            start = Blend(start, Color.FromArgb(255, 42, 57, 72), 0.58f);
            end = Blend(end, Color.FromArgb(255, 92, 110, 126), 0.58f);
        }
        else if (sky == WeatherSky.PartlyCloudy)
        {
            start = Blend(start, Color.FromArgb(255, 55, 80, 104), 0.25f);
            end = Blend(end, Color.FromArgb(255, 124, 145, 161), 0.25f);
        }

        if (effect == WeatherEffect.Thunderstorm)
        {
            start = Blend(start, Color.FromArgb(255, 21, 18, 45), 0.72f);
            end = Blend(end, Color.FromArgb(255, 51, 55, 88), 0.72f);
        }
        else if (effect == WeatherEffect.Rain)
        {
            start = Blend(start, Color.FromArgb(255, 16, 42, 67), 0.45f);
            end = Blend(end, Color.FromArgb(255, 45, 83, 112), 0.45f);
        }
        else if (effect == WeatherEffect.Snow)
        {
            start = Blend(start, Color.FromArgb(255, 48, 85, 115), 0.28f);
            end = Blend(end, Color.FromArgb(255, 153, 185, 207), 0.28f);
        }
        else if (effect == WeatherEffect.Fog)
        {
            start = Blend(start, Color.FromArgb(255, 55, 67, 78), 0.55f);
            end = Blend(end, Color.FromArgb(255, 126, 139, 148), 0.55f);
        }

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

    private void AddCelestial(Compositor compositor, bool hot, WeatherCelestial celestialKind, WeatherTimeOfDay time)
    {
        bool isMoon = celestialKind == WeatherCelestial.Moon;
        bool isTransition = time is WeatherTimeOfDay.Dawn or WeatherTimeOfDay.Evening or WeatherTimeOfDay.Dusk;
        float diameter = hot && !isMoon ? 100 : isMoon ? 58 : 76;
        ContainerVisual celestial = compositor.CreateContainerVisual();
        celestial.Size = new Vector2(diameter);
        celestial.Offset = new Vector3(-diameter * 0.65f, -diameter * 0.25f, 0);
        celestial.RelativeOffsetAdjustment = new Vector3(1, 0, 0);
        celestial.CenterPoint = new Vector3(diameter / 2, diameter / 2, 0);

        if (isMoon)
        {
            ShapeVisual moon = CreateDisc(compositor, diameter, Color.FromArgb(190, 226, 235, 255));
            celestial.Children.InsertAtTop(moon);

            for (int index = 0; index < 3; index++)
            {
                float craterSize = 5 + index * 2;
                ShapeVisual crater = CreateDisc(compositor, craterSize, Color.FromArgb(42, 94, 111, 151));
                crater.Offset = new Vector3(14 + index * 11, 18 + index % 2 * 13, 0);
                celestial.Children.InsertAtTop(crater);
            }
        }
        else
        {
            for (int index = 0; index < 3; index++)
            {
                float inset = index * 12;
                Color color = Color.FromArgb((byte)(45 + index * 40), 255, (byte)(hot ? 142 : isTransition ? 181 : 220), (byte)(isTransition ? 105 : 73));
                ShapeVisual disc = CreateDisc(compositor, diameter - inset * 2, color);
                disc.Offset = new Vector3(inset, inset, 0);
                celestial.Children.InsertAtTop(disc);
            }
        }

        Vector3KeyFrameAnimation scale = compositor.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(0, Vector3.One);
        scale.InsertKeyFrame(0.5f, new Vector3(1.08f));
        scale.InsertKeyFrame(1, Vector3.One);
        scale.Duration = TimeSpan.FromSeconds(4);
        scale.IterationBehavior = AnimationIterationBehavior.Forever;
        celestial.StartAnimation("Scale", scale);
        activeSceneLayer?.Children.InsertAtTop(celestial);
    }

    private void AddClouds(Compositor compositor, int count)
    {
        for (int index = 0; index < count; index++)
        {
            float width = RandomBetween(68, 104);
            float height = width * 0.4f;
            byte alpha = (byte)RandomBetween(80, 145);
            Path cloud = CreateCloud(width, height, alpha);
            Canvas.SetLeft(cloud, -width);
            Canvas.SetTop(cloud, 6 + index * 28);
            activeCloudLayer?.Children.Add(cloud);
            ElementCompositionPreview.SetIsTranslationEnabled(cloud, true);
            Visual cloudVisual = ElementCompositionPreview.GetElementVisual(cloud);
            float phase = (float)(index + 1) / (count + 1);
            float travel = SceneWidth + width * 2;
            float initialTranslation = travel * phase;
            StartInitialCloudDrift(compositor, cloud, cloudVisual, initialTranslation, travel);
        }
    }

    private void StartInitialCloudDrift(Compositor compositor, Path cloud, Visual cloudVisual, float initialTranslation, float travel)
    {
        CompositionScopedBatch batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        ScalarKeyFrameAnimation drift = compositor.CreateScalarKeyFrameAnimation();
        drift.InsertKeyFrame(0, initialTranslation);
        drift.InsertKeyFrame(1, travel);
        drift.Duration = TimeSpan.FromSeconds(42 * (1 - initialTranslation / travel));
        cloudVisual.StartAnimation("Translation.X", drift);
        batch.End();
        batch.Completed += (sender, args) => DispatcherQueue.TryEnqueue(() =>
        {
            batch.Dispose();

            if (!IsLoaded || activeCloudLayer?.Children.Contains(cloud) != true)
            {
                return;
            }

            ScalarKeyFrameAnimation loop = compositor.CreateScalarKeyFrameAnimation();
            loop.InsertKeyFrame(0, 0);
            loop.InsertKeyFrame(1, travel);
            loop.Duration = TimeSpan.FromSeconds(42);
            loop.IterationBehavior = AnimationIterationBehavior.Forever;
            cloudVisual.StartAnimation("Translation.X", loop);
        });
    }

    private void AddStars(Compositor compositor)
    {
        for (int index = 0; index < 10; index++)
        {
            float diameter = RandomBetween(1.2f, 2.8f);
            ShapeVisual star = CreateDisc(compositor, diameter, Color.FromArgb((byte)RandomBetween(95, 190), 232, 240, 255));
            star.Offset = new Vector3(RandomBetween(18, SceneWidth - 28), RandomBetween(8, SceneHeight * 0.65f), 0);
            ScalarKeyFrameAnimation twinkle = compositor.CreateScalarKeyFrameAnimation();
            twinkle.InsertKeyFrame(0, 0.35f);
            twinkle.InsertKeyFrame(0.5f, 1);
            twinkle.InsertKeyFrame(1, 0.35f);
            twinkle.Duration = TimeSpan.FromSeconds(RandomBetween(2.5f, 5));
            twinkle.DelayTime = TimeSpan.FromSeconds(RandomBetween(0, 2));
            twinkle.IterationBehavior = AnimationIterationBehavior.Forever;
            star.StartAnimation("Opacity", twinkle);
            activeSceneLayer?.Children.InsertAtTop(star);
        }
    }

    private static Color Blend(Color source, Color target, float amount) =>
        Color.FromArgb(255,
            (byte)(source.R + (target.R - source.R) * amount),
            (byte)(source.G + (target.G - source.G) * amount),
            (byte)(source.B + (target.B - source.B) * amount));

    private static Path CreateCloud(float width, float height, byte alpha)
    {
        PathFigure figure = new()
        {
            StartPoint = new Point(14, 84),
            IsClosed = true,
            IsFilled = true,
            Segments =
            {
                new BezierSegment { Point1 = new Point(6, 84), Point2 = new Point(0, 76), Point3 = new Point(0, 63) },
                new BezierSegment { Point1 = new Point(0, 49), Point2 = new Point(10, 37), Point3 = new Point(24, 36) },
                new BezierSegment { Point1 = new Point(29, 15), Point2 = new Point(45, 4), Point3 = new Point(61, 12) },
                new BezierSegment { Point1 = new Point(71, 17), Point2 = new Point(77, 28), Point3 = new Point(79, 40) },
                new BezierSegment { Point1 = new Point(91, 40), Point2 = new Point(100, 50), Point3 = new Point(100, 64) },
                new BezierSegment { Point1 = new Point(100, 78), Point2 = new Point(90, 84), Point3 = new Point(77, 84) }
            }
        };
        PathGeometry geometry = new();
        geometry.Figures.Add(figure);
        return new Path
        {
            Width = width,
            Height = height,
            Data = geometry,
            Fill = new SolidColorBrush(Color.FromArgb(alpha, 234, 242, 248)),
            Stretch = Stretch.Fill,
            IsHitTestVisible = false
        };
    }

    private void AddRain(Compositor compositor, int count)
    {
        for (int index = 0; index < count; index++)
        {
            SpriteVisual drop = compositor.CreateSpriteVisual();
            drop.Brush = compositor.CreateColorBrush(Color.FromArgb((byte)RandomBetween(75, 160), 158, 214, 255));
            drop.Size = new Vector2(1.2f, RandomBetween(8, 15));
            drop.Offset = new Vector3(RandomBetween(0, SceneWidth + 50), RandomBetween(-SceneHeight, 0), 0);
            ScalarKeyFrameAnimation fall = compositor.CreateScalarKeyFrameAnimation();
            fall.InsertKeyFrame(0, -SceneHeight * 0.2f);
            fall.InsertKeyFrame(1, SceneHeight + 20);
            fall.Duration = TimeSpan.FromSeconds(RandomBetween(0.65f, 1.15f));
            fall.DelayTime = TimeSpan.FromSeconds(RandomBetween(0, 1));
            fall.IterationBehavior = AnimationIterationBehavior.Forever;
            drop.StartAnimation("Offset.Y", fall);
            activeSceneLayer?.Children.InsertAtTop(drop);
        }
    }

    private void AddSnow(Compositor compositor, int count)
    {
        for (int index = 0; index < count; index++)
        {
            float size = RandomBetween(2.5f, 6);
            ShapeVisual flake = CreateDisc(compositor, size, Color.FromArgb((byte)RandomBetween(100, 220), 248, 252, 255));
            flake.Offset = new Vector3(RandomBetween(0, SceneWidth), RandomBetween(-SceneHeight, 0), 0);
            ScalarKeyFrameAnimation fall = compositor.CreateScalarKeyFrameAnimation();
            fall.InsertKeyFrame(0, -SceneHeight * 0.15f);
            fall.InsertKeyFrame(1, SceneHeight + 10);
            fall.Duration = TimeSpan.FromSeconds(RandomBetween(3, 6));
            fall.DelayTime = TimeSpan.FromSeconds(RandomBetween(0, 2));
            fall.IterationBehavior = AnimationIterationBehavior.Forever;
            flake.StartAnimation("Offset.Y", fall);
            activeSceneLayer?.Children.InsertAtTop(flake);
        }
    }

    private void AddLightning(Compositor compositor)
    {
        lightningFlash = compositor.CreateSpriteVisual();
        lightningFlash.RelativeSizeAdjustment = Vector2.One;
        lightningFlash.Brush = compositor.CreateColorBrush(Color.FromArgb(255, 220, 229, 255));
        lightningFlash.Opacity = 0;
        activeSceneLayer?.Children.InsertAtTop(lightningFlash);

        lightningTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        lightningTimer.IsRepeating = true;
        lightningTimer.Tick += HandleLightningTimer;
        ScheduleNextLightning();
        lightningTimer.Start();
        TriggerLightning();
    }

    private void HandleLightningTimer(DispatcherQueueTimer sender, object args)
    {
        TriggerLightning();
        ScheduleNextLightning();
    }

    private void TriggerLightning()
    {
        if (!IsLoaded || lightningFlash is null)
        {
            return;
        }

        LightningHost.Children.Clear();
        PathGeometry geometry = CreateLightningGeometry();
        Path core = new()
        {
            Data = geometry,
            Stroke = new SolidColorBrush(Color.FromArgb(245, 239, 245, 255)),
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round,
            IsHitTestVisible = false,
            Opacity = 0
        };
        LightningHost.Children.Add(core);
        Compositor compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        StartStrikeAnimation(compositor, ElementCompositionPreview.GetElementVisual(core), 1);
        StartStrikeAnimation(compositor, lightningFlash, 0.24f);
    }

    private PathGeometry CreateLightningGeometry()
    {
        double x = RandomBetween(SceneWidth * 0.16f, SceneWidth * 0.84f);
        double y = -6;
        double segmentHeight = RandomBetween(13, 21);
        PathFigure main = new() { StartPoint = new Point(x, y) };
        PathGeometry geometry = new();
        geometry.Figures.Add(main);
        int segments = random.Next(5, 8);

        for (int index = 0; index < segments; index++)
        {
            x += RandomBetween(-16, 16);
            y += segmentHeight * RandomBetween(0.8f, 1.2f);
            Point point = new(x, y);
            main.Segments.Add(new LineSegment { Point = point });

            if (index > 1 && index < segments - 1 && random.NextDouble() < 0.42)
            {
                PathFigure branch = new() { StartPoint = point };
                double branchX = x;
                double branchY = y;

                for (int branchIndex = 0; branchIndex < random.Next(2, 4); branchIndex++)
                {
                    branchX += RandomBetween(-22, 22);
                    branchY += RandomBetween(8, 15);
                    branch.Segments.Add(new LineSegment { Point = new Point(branchX, branchY) });
                }

                geometry.Figures.Add(branch);
            }
        }

        return geometry;
    }

    private static void StartStrikeAnimation(Compositor compositor, Visual visual, float peakOpacity)
    {
        ScalarKeyFrameAnimation pulse = compositor.CreateScalarKeyFrameAnimation();
        pulse.InsertKeyFrame(0, 0);
        pulse.InsertKeyFrame(0.08f, peakOpacity);
        pulse.InsertKeyFrame(0.18f, peakOpacity * 0.08f);
        pulse.InsertKeyFrame(0.28f, peakOpacity * 0.88f);
        pulse.InsertKeyFrame(0.44f, 0);
        pulse.InsertKeyFrame(1, 0);
        pulse.Duration = TimeSpan.FromMilliseconds(720);
        visual.StartAnimation("Opacity", pulse);
    }

    private void ScheduleNextLightning()
    {
        if (lightningTimer is not null)
        {
            lightningTimer.Interval = TimeSpan.FromSeconds(RandomBetween(2.4f, 7.5f));
        }
    }

    private void StopLightning()
    {
        if (lightningTimer is not null)
        {
            lightningTimer.Stop();
            lightningTimer.Tick -= HandleLightningTimer;
            lightningTimer = null;
        }

        LightningHost.Children.Clear();
        lightningFlash = null;
    }

    private void AddFog(Compositor compositor)
    {
        for (int index = 0; index < 4; index++)
        {
            Vector2 size = new(SceneWidth * RandomBetween(0.75f, 1.05f), RandomBetween(18, 30));
            ShapeVisual band = CreateRoundedRectangle(compositor, size, Color.FromArgb((byte)(18 + index * 7), 230, 238, 243));
            band.Offset = new Vector3(-SceneWidth * 0.15f, 8 + index * 25, 0);
            ScalarKeyFrameAnimation drift = compositor.CreateScalarKeyFrameAnimation();
            drift.InsertKeyFrame(0, -SceneWidth * 0.12f);
            drift.InsertKeyFrame(0.5f, SceneWidth * 0.08f);
            drift.InsertKeyFrame(1, -SceneWidth * 0.12f);
            drift.Duration = TimeSpan.FromSeconds(RandomBetween(12, 18));
            drift.IterationBehavior = AnimationIterationBehavior.Forever;
            band.StartAnimation("Offset.X", drift);
            activeSceneLayer?.Children.InsertAtTop(band);
        }
    }

    private void AddHaze(Compositor compositor)
    {
        for (int index = 0; index < 5; index++)
        {
            SpriteVisual haze = compositor.CreateSpriteVisual();
            haze.Brush = compositor.CreateColorBrush(Color.FromArgb(35, 255, 228, 150));
            haze.Size = new Vector2(SceneWidth * 0.65f, 1.5f);
            haze.Offset = new Vector3(SceneWidth * 0.15f, 34 + index * 13, 0);
            ScalarKeyFrameAnimation shimmer = compositor.CreateScalarKeyFrameAnimation();
            shimmer.InsertKeyFrame(0, 0.15f);
            shimmer.InsertKeyFrame(0.5f, 0.7f);
            shimmer.InsertKeyFrame(1, 0.15f);
            shimmer.Duration = TimeSpan.FromSeconds(2 + index * 0.3);
            shimmer.IterationBehavior = AnimationIterationBehavior.Forever;
            haze.StartAnimation("Opacity", shimmer);
            activeSceneLayer?.Children.InsertAtTop(haze);
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

    private static ShapeVisual CreateRoundedRectangle(Compositor compositor, Vector2 size, Color color)
    {
        ShapeVisual visual = compositor.CreateShapeVisual();
        visual.Size = size;
        CompositionRoundedRectangleGeometry geometry = compositor.CreateRoundedRectangleGeometry();
        geometry.Size = size;
        geometry.CornerRadius = new Vector2(size.Y / 2);
        CompositionSpriteShape shape = compositor.CreateSpriteShape(geometry);
        shape.FillBrush = compositor.CreateColorBrush(color);
        visual.Shapes.Add(shape);
        return visual;
    }

    private float RandomBetween(float minimum, float maximum) =>
        minimum + (float)random.NextDouble() * (maximum - minimum);

    private float SceneWidth => Math.Max((float)ActualWidth, 380);

    private float SceneHeight => Math.Max((float)ActualHeight, 120);

    private readonly record struct WeatherSceneState(WeatherTimeOfDay Time,
        WeatherSky Sky,
        WeatherCelestial Celestial,
        WeatherEffect Effect,
        WeatherTemperature Temperature);
}
