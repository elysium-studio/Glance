using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;

namespace Glance.Media.WinUI;

public sealed partial class MediaAudioVisualizer : UserControl
{
    private FrameworkElement[]? bars;
    private MediaViewModel? viewModel;

    public MediaAudioVisualizer() => InitializeComponent();

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
        }
    }

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        bars = [BarOne, BarTwo, BarThree, BarFour, BarFive];

        foreach (FrameworkElement bar in bars)
        {
            Visual visual = ElementCompositionPreview.GetElementVisual(bar);
            visual.CenterPoint = new Vector3((float)bar.ActualWidth / 2, (float)bar.ActualHeight, 0);
            visual.Scale = new Vector3(1, 0.16f, 1);
        }

        Subscribe();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        Unsubscribe();
        bars = null;
    }

    private void Subscribe()
    {
        if (IsLoaded && viewModel is not null)
        {
            viewModel.AudioLevelsChanged -= HandleAudioLevelsChanged;
            viewModel.AudioLevelsChanged += HandleAudioLevelsChanged;
        }
    }

    private void Unsubscribe()
    {
        if (viewModel is not null)
        {
            viewModel.AudioLevelsChanged -= HandleAudioLevelsChanged;
        }
    }

    private void HandleAudioLevelsChanged(object? sender, AudioLevelsChangedEventArgs args)
    {
        if (bars is null)
        {
            return;
        }

        for (int index = 0; index < bars.Length; index++)
        {
            double level = index < args.Levels.Count ? args.Levels[index] : 0;
            AnimateBar(bars[index], level);
        }
    }

    private static void AnimateBar(FrameworkElement bar, double level)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(bar);
        Compositor compositor = visual.Compositor;
        ScalarKeyFrameAnimation animation = compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = TimeSpan.FromMilliseconds(90);
        animation.InsertKeyFrame(1, (float)(0.16 + (Math.Clamp(level, 0, 1) * 0.84)), compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.8f), new Vector2(0.2f, 1)));
        visual.StartAnimation("Scale.Y", animation);
    }
}
