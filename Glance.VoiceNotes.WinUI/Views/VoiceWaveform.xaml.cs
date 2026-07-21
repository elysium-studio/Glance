using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Numerics;

namespace Glance.VoiceNotes.WinUI;

public sealed partial class VoiceWaveform :
    UserControl
{
    private FrameworkElement[]? bars;
    private VoiceNotesViewModel? viewModel;

    public VoiceWaveform() => InitializeComponent();

    public int BarCount { get; set; } = 42;

    public VoiceNotesViewModel? ViewModel
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
        double barHeight = Math.Max(2, ActualHeight - 2);
        Brush waveformBrush = (Brush)Resources["GlanceVoiceNotesIconBrush"];
        bars = new FrameworkElement[Math.Max(1, BarCount)];
        WaveformPanel.Children.Clear();

        for (int index = 0; index < bars.Length; index++)
        {
            Border bar = new()
            {
                Width = 1,
                Height = barHeight,
                CornerRadius = new CornerRadius(0.5)
            };
            bar.Background = waveformBrush;
            WaveformPanel.Children.Add(bar);
            bars[index] = bar;

            Visual visual = ElementCompositionPreview.GetElementVisual(bar);
            visual.CenterPoint = new Vector3(
                0.5f,
                (float)barHeight / 2,
                0);
            visual.Scale = new Vector3(1, 0.04f, 1);
        }

        ActualThemeChanged -= HandleActualThemeChanged;
        ActualThemeChanged += HandleActualThemeChanged;
        Subscribe();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        ActualThemeChanged -= HandleActualThemeChanged;
        Unsubscribe();
        bars = null;
    }

    private void HandleActualThemeChanged(
        FrameworkElement sender,
        object args)
    {
        if (bars is null)
        {
            return;
        }

        Brush waveformBrush = (Brush)Resources["GlanceVoiceNotesIconBrush"];

        foreach (FrameworkElement bar in bars)
        {
            ((Border)bar).Background = waveformBrush;
        }
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

    private void HandleAudioLevelsChanged(
        object? sender,
        VoiceLevelsChangedEventArgs args)
    {
        if (bars is null)
        {
            return;
        }

        int sourceOffset = Math.Max(0, args.Levels.Count - bars.Length);

        for (int index = 0; index < bars.Length; index++)
        {
            int sourceIndex = sourceOffset + index;
            double level = sourceIndex < args.Levels.Count
                ? args.Levels[sourceIndex]
                : 0;
            AnimateBar(bars[index], level);
        }
    }

    private static void AnimateBar(FrameworkElement bar, double level)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(bar);
        Compositor compositor = visual.Compositor;
        ScalarKeyFrameAnimation animation =
            compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = TimeSpan.FromMilliseconds(75);
        animation.InsertKeyFrame(
            1,
            (float)(0.04 + (Math.Clamp(level, 0, 1) * 0.96)),
            compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.2f, 0.8f),
                new Vector2(0.2f, 1)));
        visual.StartAnimation("Scale.Y", animation);
    }
}
