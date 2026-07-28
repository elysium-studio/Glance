using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Numerics;

namespace Glance.UI.WinUI;

public sealed class CompositionActivityPulse
{
    private readonly FrameworkElement owner;
    private readonly FrameworkElement ring;
    private readonly string propertyName;
    private readonly Func<bool> isActive;
    private EventHandler<object>? loadedRenderingHandler;
    private Visual? visual;
    private bool isRunning;

    public CompositionActivityPulse(FrameworkElement owner,
        FrameworkElement ring,
        INotifyPropertyChanged source,
        string propertyName,
        Func<bool> isActive)
    {
        this.owner = owner;
        this.ring = ring;
        this.propertyName = propertyName;
        this.isActive = isActive;
        source.PropertyChanged += OnPropertyChanged;
        ring.Loaded += OnLoaded;
        ring.Unloaded += OnUnloaded;
        _ = GetVisual();

        if (ring.IsLoaded)
        {
            Refresh();
        }
    }

    public void Refresh() =>
        owner.DispatcherQueue.TryEnqueue(Update);

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        Stop();
        ScheduleLoadedRefresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        CancelLoadedRefresh();
        Stop();
    }

    private void ScheduleLoadedRefresh()
    {
        CancelLoadedRefresh();
        int preparationFrames = 0;
        loadedRenderingHandler = (_, _) =>
        {
            preparationFrames++;

            if (preparationFrames < 2)
            {
                return;
            }

            CancelLoadedRefresh();
            Update();
        };
        CompositionTarget.Rendering += loadedRenderingHandler;
    }

    private void CancelLoadedRefresh()
    {
        if (loadedRenderingHandler is null)
        {
            return;
        }

        CompositionTarget.Rendering -= loadedRenderingHandler;
        loadedRenderingHandler = null;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != propertyName)
        {
            return;
        }

        Refresh();
    }

    private void Update()
    {
        if (!ring.IsLoaded)
        {
            Stop();
            return;
        }

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

        Visual pulseVisual = GetVisual();
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

    private Visual GetVisual()
    {
        if (visual is not null)
        {
            return visual;
        }

        visual = ElementCompositionPreview.GetElementVisual(ring);
        float width = (float)(ring.ActualWidth > 0 ? ring.ActualWidth : ring.Width);
        float height = (float)(ring.ActualHeight > 0 ? ring.ActualHeight : ring.Height);
        visual.CenterPoint = new(width / 2, height / 2, 0);
        visual.Scale = Vector3.One;
        visual.Opacity = 0;
        return visual;
    }

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
