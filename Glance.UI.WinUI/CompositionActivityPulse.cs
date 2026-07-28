using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.ComponentModel;
using System.Numerics;

namespace Glance.UI.WinUI;

public sealed class CompositionActivityPulse
{
    private readonly FrameworkElement ring;
    private readonly INotifyPropertyChanged source;
    private readonly string propertyName;
    private readonly Func<bool> isActive;
    private Visual? visual;
    private bool isRunning;
    private bool isSubscribed;

    public CompositionActivityPulse(FrameworkElement owner,
        FrameworkElement ring,
        INotifyPropertyChanged source,
        string propertyName,
        Func<bool> isActive)
    {
        this.ring = ring;
        this.source = source;
        this.propertyName = propertyName;
        this.isActive = isActive;
        owner.Loaded += OnLoaded;
        owner.Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (!isSubscribed)
        {
            source.PropertyChanged += OnPropertyChanged;
            isSubscribed = true;
        }

        Update();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (isSubscribed)
        {
            source.PropertyChanged -= OnPropertyChanged;
            isSubscribed = false;
        }

        Stop();
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == propertyName)
        {
            Update();
        }
    }

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

        visual ??= ElementCompositionPreview.GetElementVisual(ring);
        visual.CenterPoint = new((float)ring.ActualWidth / 2, (float)ring.ActualHeight / 2, 0);
        visual.Scale = Vector3.One;
        visual.Opacity = 0;

        Compositor compositor = visual.Compositor;
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

        visual.StartAnimation(nameof(Visual.Scale), scale);
        visual.StartAnimation(nameof(Visual.Opacity), opacity);
        isRunning = true;
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
