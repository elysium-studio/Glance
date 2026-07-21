using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Numerics;

namespace Glance.PrivacyControls.WinUI;

public sealed partial class MicrophoneLevelMeter :
    UserControl
{
    private static readonly double[] BarFactors = [0.46, 0.72, 0.9, 0.62, 1, 0.78, 0.54];

    private FrameworkElement[]? bars;
    private PrivacyControlsViewModel? viewModel;

    public MicrophoneLevelMeter() =>
        InitializeComponent();

    public int BarCount { get; set; } = 12;

    public PrivacyControlsViewModel? ViewModel
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
        Brush levelBrush = (Brush)Resources["GlancePrivacyControlsIconBrush"];
        bars = new FrameworkElement[Math.Max(1, BarCount)];
        LevelPanel.Children.Clear();
        LevelPanel.ColumnDefinitions.Clear();

        for (int index = 0; index < bars.Length; index++)
        {
            LevelPanel.ColumnDefinitions.Add(new ColumnDefinition());
            Border bar = new()
            {
                Width = 2,
                Height = 2,
                Background = levelBrush,
                CornerRadius = new CornerRadius(1)
            };
            Grid.SetColumn(bar, index);
            LevelPanel.Children.Add(bar);
            bars[index] = bar;

            Visual visual = ElementCompositionPreview.GetElementVisual(bar);
            visual.CenterPoint = new Vector3(1, 1, 0);
            visual.Scale = new Vector3(1, 0.08f, 1);
        }

        UpdateBarGeometry();
        ActualThemeChanged -= HandleActualThemeChanged;
        ActualThemeChanged += HandleActualThemeChanged;
        Subscribe();
    }

    private void HandleSizeChanged(
        object sender,
        SizeChangedEventArgs args) =>
        UpdateBarGeometry();

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

        Brush levelBrush = (Brush)Resources["GlancePrivacyControlsIconBrush"];

        foreach (FrameworkElement bar in bars)
        {
            ((Border)bar).Background = levelBrush;
        }
    }

    private void UpdateBarGeometry()
    {
        if (bars is null || ActualHeight <= 0)
        {
            return;
        }

        double barHeight = Math.Max(2, ActualHeight - 2);

        foreach (FrameworkElement bar in bars)
        {
            bar.Height = barHeight;
            ElementCompositionPreview.GetElementVisual(bar).CenterPoint = new Vector3(1, (float)barHeight / 2, 0);
        }
    }

    private void Subscribe()
    {
        if (IsLoaded && viewModel is not null)
        {
            viewModel.LevelChanged -= HandleLevelChanged;
            viewModel.LevelChanged += HandleLevelChanged;
        }
    }

    private void Unsubscribe()
    {
        if (viewModel is not null)
        {
            viewModel.LevelChanged -= HandleLevelChanged;
        }
    }

    private void HandleLevelChanged(
        object? sender,
        MicrophoneLevelChangedEventArgs args)
    {
        if (bars is null)
        {
            return;
        }

        for (int index = 0; index < bars.Length; index++)
        {
            AnimateBar(bars[index], args.Level * BarFactors[index % BarFactors.Length]);
        }
    }

    private static void AnimateBar(FrameworkElement bar, double level)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(bar);
        Compositor compositor = visual.Compositor;
        ScalarKeyFrameAnimation animation = compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = TimeSpan.FromMilliseconds(90);
        animation.InsertKeyFrame(1, (float)(0.08 + (Math.Clamp(level, 0, 1) * 0.92)), compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.8f), new Vector2(0.2f, 1)));
        visual.StartAnimation("Scale.Y", animation);
    }
}
