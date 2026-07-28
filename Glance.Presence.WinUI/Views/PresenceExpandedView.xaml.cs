using Glance.UI.WinUI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.ComponentModel;
using System.Numerics;

namespace Glance.Presence.WinUI;

public sealed partial class PresenceExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<PresenceModule> localizer;
    private Visual? pulseVisual;
    private bool isPulseRunning;

    public PresenceExpandedView(PresenceViewModel viewModel,
        ModuleResourceTextLocalizer<PresenceModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public PresenceViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("ModuleDisplayName");

    private bool IsActionEnabled(bool isBusy) =>
        !isBusy;

    private void OnLoaded(object sender,
        RoutedEventArgs args)
    {
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdatePulse();
    }

    private void OnUnloaded(object sender,
        RoutedEventArgs args)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        StopPulse();
    }

    private void OnViewModelPropertyChanged(object? sender,
        PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(PresenceViewModel.IsActive))
        {
            UpdatePulse();
        }
    }

    private void UpdatePulse()
    {
        if (ViewModel.IsActive)
        {
            StartPulse();
        }
        else
        {
            StopPulse();
        }
    }

    private void StartPulse()
    {
        if (isPulseRunning)
        {
            return;
        }

        pulseVisual ??= ElementCompositionPreview.GetElementVisual(PulseRing);
        pulseVisual.CenterPoint = new((float)PulseRing.ActualWidth / 2,
            (float)PulseRing.ActualHeight / 2,
            0);
        pulseVisual.Scale = Vector3.One;
        pulseVisual.Opacity = 0;

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
        isPulseRunning = true;
    }

    private void StopPulse()
    {
        if (pulseVisual is null)
        {
            return;
        }

        pulseVisual.StopAnimation(nameof(Visual.Scale));
        pulseVisual.StopAnimation(nameof(Visual.Opacity));
        pulseVisual.Scale = Vector3.One;
        pulseVisual.Opacity = 0;
        isPulseRunning = false;
    }

    private string ToUpper(string value) =>
        value.ToUpperInvariant();
}
