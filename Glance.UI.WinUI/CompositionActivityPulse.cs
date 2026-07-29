using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.ComponentModel;
using System.Numerics;
using Windows.UI;

namespace Glance.UI.WinUI;

public sealed class CompositionActivityPulse
{
    private readonly FrameworkElement owner;
    private readonly Shape ring;
    private readonly string propertyName;
    private readonly Func<bool> isActive;
    private CompositionColorBrush? pulseBrush;
    private ShapeVisual? visual;
    private bool isRunning;

    public CompositionActivityPulse(FrameworkElement owner,
        Shape ring,
        INotifyPropertyChanged source,
        string propertyName,
        Func<bool> isActive)
    {
        this.owner = owner;
        this.ring = ring;
        this.propertyName = propertyName;
        this.isActive = isActive;
        source.PropertyChanged += OnPropertyChanged;
        owner.ActualThemeChanged += OnActualThemeChanged;
        _ = GetVisual();
        Update();
    }

    public void Refresh() =>
        owner.DispatcherQueue.TryEnqueue(Update);

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != propertyName)
        {
            return;
        }

        Refresh();
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args) =>
        owner.DispatcherQueue.TryEnqueue(UpdateBrush);

    private void Update()
    {
        if (isActive())
        {
            Start();
        }
        else
        {
            Stop();
        }
    }

    private void Start()
    {
        if (isRunning)
        {
            return;
        }

        ShapeVisual pulseVisual = GetVisual();
        Compositor compositor = pulseVisual.Compositor;
        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(new(.16f, 1), new(.3f, 1));
        Vector3KeyFrameAnimation scale = compositor.CreateVector3KeyFrameAnimation();
        scale.Duration = TimeSpan.FromMilliseconds(1400);
        scale.IterationBehavior = AnimationIterationBehavior.Forever;
        scale.InsertKeyFrame(0, new(.92f, .92f, 1));
        scale.InsertKeyFrame(1, new(1.45f, 1.45f, 1), easing);

        ScalarKeyFrameAnimation opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.Duration = scale.Duration;
        opacity.IterationBehavior = AnimationIterationBehavior.Forever;
        opacity.InsertKeyFrame(0, 0);
        opacity.InsertKeyFrame(.12f, .58f);
        opacity.InsertKeyFrame(1, 0, easing);

        pulseVisual.StartAnimation(nameof(Visual.Scale), scale);
        pulseVisual.StartAnimation(nameof(Visual.Opacity), opacity);
        isRunning = true;
    }

    private ShapeVisual GetVisual()
    {
        if (visual is not null)
        {
            return visual;
        }

        Compositor compositor = ElementCompositionPreview.GetElementVisual(ring).Compositor;
        float width = (float)(ring.ActualWidth > 0 ? ring.ActualWidth : ring.Width);
        float height = (float)(ring.ActualHeight > 0 ? ring.ActualHeight : ring.Height);
        CompositionEllipseGeometry geometry = compositor.CreateEllipseGeometry();
        geometry.Center = new(width / 2, height / 2);
        geometry.Radius = new(MathF.Max(0, width / 2 - .75f), MathF.Max(0, height / 2 - .75f));
        pulseBrush = compositor.CreateColorBrush(GetColor());
        CompositionSpriteShape sprite = compositor.CreateSpriteShape(geometry);
        sprite.StrokeBrush = pulseBrush;
        sprite.StrokeThickness = 1.5f;
        visual = compositor.CreateShapeVisual();
        visual.Size = new(width, height);
        visual.Shapes.Add(sprite);
        visual.CenterPoint = new(width / 2, height / 2, 0);
        visual.Scale = Vector3.One;
        visual.Opacity = 0;
        ring.StrokeThickness = 0;
        ElementCompositionPreview.SetElementChildVisual(ring, visual);
        return visual;
    }

    private void UpdateBrush()
    {
        if (pulseBrush is not null)
        {
            pulseBrush.Color = GetColor();
        }
    }

    private Color GetColor() =>
        ring.Stroke is SolidColorBrush brush
            ? brush.Color
            : Color.FromArgb(255, 255, 255, 255);

    private void Stop()
    {
        if (visual is null)
        {
            return;
        }

        visual.StopAnimation(nameof(Visual.Scale));
        visual.StopAnimation(nameof(Visual.Opacity));
        visual.Scale = Vector3.One;
        visual.Opacity = 0;
        isRunning = false;
    }
}
