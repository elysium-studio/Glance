using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Glance.SystemMonitor.WinUI;

public sealed partial class SystemMetricGraph :
    Grid
{
    private const int SampleCapacity = 48;
    private const int GridColumnCount = 6;
    private const int GridRowCount = 3;
    private static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(280);
    private readonly double[] primarySamples = new double[SampleCapacity];
    private readonly double[] secondarySamples = new double[SampleCapacity];
    private readonly List<SpriteVisual> primarySegments = [];
    private readonly List<SpriteVisual> secondarySegments = [];
    private readonly List<SpriteVisual> gridLines = [];
    private ContainerVisual? rootVisual;
    private ContainerVisual? primaryVisual;
    private ContainerVisual? secondaryVisual;
    private CompositionColorBrush? primaryBrush;
    private CompositionColorBrush? secondaryBrush;
    private CompositionColorBrush? gridBrush;
    private Compositor? compositor;
    private double dynamicMaximum = 1024;

    public SystemMetricGraph()
    {
        IsHitTestVisible = false;
        Loaded += HandleLoaded;
        SizeChanged += HandleSizeChanged;
        ActualThemeChanged += HandleActualThemeChanged;
    }

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(SystemMetricGraph), new PropertyMetadata(100d, HandleAppearanceChanged));

    public static readonly DependencyProperty PrimaryStrokeProperty = DependencyProperty.Register(nameof(PrimaryStroke), typeof(Brush), typeof(SystemMetricGraph), new PropertyMetadata(null, HandleAppearanceChanged));

    public static readonly DependencyProperty SecondaryStrokeProperty = DependencyProperty.Register(nameof(SecondaryStroke), typeof(Brush), typeof(SystemMetricGraph), new PropertyMetadata(null, HandleAppearanceChanged));

    public static readonly DependencyProperty GridStrokeProperty = DependencyProperty.Register(nameof(GridStroke), typeof(Brush), typeof(SystemMetricGraph), new PropertyMetadata(null, HandleAppearanceChanged));

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public Brush PrimaryStroke
    {
        get => (Brush)GetValue(PrimaryStrokeProperty);
        set => SetValue(PrimaryStrokeProperty, value);
    }

    public Brush? SecondaryStroke
    {
        get => (Brush?)GetValue(SecondaryStrokeProperty);
        set => SetValue(SecondaryStrokeProperty, value);
    }

    public Brush GridStroke
    {
        get => (Brush)GetValue(GridStrokeProperty);
        set => SetValue(GridStrokeProperty, value);
    }

    public void AddSample(double primary, double? secondary = null)
    {
        double primaryValue = Math.Max(0, primary);
        double secondaryValue = Math.Max(0, secondary ?? 0);

        if (Maximum <= 0)
        {
            double currentMaximum = Math.Max(Math.Max(primarySamples.Max(), secondarySamples.Max()), Math.Max(primaryValue, secondaryValue));
            dynamicMaximum = GetDynamicMaximum(currentMaximum);
        }

        if (IsLoaded && ActualWidth > 0 && ActualHeight > 0)
        {
            EnsureVisuals();
            RenderNextSample(primarySamples, primaryValue, primarySegments, primaryVisual, 2);
            RenderNextSample(secondarySamples, secondaryValue, secondarySegments, secondaryVisual, 1.5f);
        }

        ShiftSamples(primarySamples, primaryValue);
        ShiftSamples(secondarySamples, secondaryValue);
    }

    private static void ShiftSamples(double[] samples, double value)
    {
        Array.Copy(samples, 1, samples, 0, samples.Length - 1);
        samples[^1] = value;
    }

    private static void HandleAppearanceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is SystemMetricGraph graph)
        {
            graph.UpdateBrushes();
            graph.Render();
        }
    }

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        EnsureVisuals();
        Render();
    }

    private void HandleSizeChanged(object sender, SizeChangedEventArgs args) => Render();

    private void HandleActualThemeChanged(FrameworkElement sender, object args)
    {
        UpdateBrushes();
        Render();
    }

    private void EnsureVisuals()
    {
        if (rootVisual is not null)
        {
            return;
        }

        compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        rootVisual = compositor.CreateContainerVisual();
        rootVisual.Clip = compositor.CreateInsetClip();
        primaryVisual = compositor.CreateContainerVisual();
        secondaryVisual = compositor.CreateContainerVisual();
        rootVisual.Children.InsertAtTop(primaryVisual);
        rootVisual.Children.InsertAtTop(secondaryVisual);
        ElementCompositionPreview.SetElementChildVisual(this, rootVisual);

        for (int index = 0; index < GridColumnCount - 1 + GridRowCount - 1; index++)
        {
            SpriteVisual line = compositor.CreateSpriteVisual();
            gridLines.Add(line);
            rootVisual.Children.InsertAtBottom(line);
        }

        for (int index = 0; index < SampleCapacity; index++)
        {
            SpriteVisual primarySegment = compositor.CreateSpriteVisual();
            primarySegments.Add(primarySegment);
            primaryVisual.Children.InsertAtTop(primarySegment);

            SpriteVisual secondarySegment = compositor.CreateSpriteVisual();
            secondarySegments.Add(secondarySegment);
            secondaryVisual.Children.InsertAtTop(secondarySegment);
        }

        UpdateBrushes();
    }

    private void UpdateBrushes()
    {
        if (compositor is null)
        {
            return;
        }

        primaryBrush?.Dispose();
        secondaryBrush?.Dispose();
        gridBrush?.Dispose();
        primaryBrush = compositor.CreateColorBrush(GetColor(PrimaryStroke));
        secondaryBrush = compositor.CreateColorBrush(GetColor(SecondaryStroke));
        gridBrush = compositor.CreateColorBrush(GetColor(GridStroke));

        foreach (SpriteVisual segment in primarySegments)
        {
            segment.Brush = primaryBrush;
        }

        foreach (SpriteVisual segment in secondarySegments)
        {
            segment.Brush = secondaryBrush;
            segment.IsVisible = SecondaryStroke is not null;
        }

        foreach (SpriteVisual line in gridLines)
        {
            line.Brush = gridBrush;
        }
    }

    private void Render()
    {
        if (!IsLoaded)
        {
            return;
        }

        EnsureVisuals();

        if (rootVisual is null || compositor is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        float width = (float)ActualWidth;
        float height = (float)ActualHeight;
        rootVisual.Size = new Vector2(width, height);
        primaryVisual!.Size = new Vector2(width, height);
        secondaryVisual!.Size = new Vector2(width, height);
        primaryVisual.StopAnimation(nameof(primaryVisual.Offset));
        secondaryVisual.StopAnimation(nameof(secondaryVisual.Offset));
        primaryVisual.Offset = Vector3.Zero;
        secondaryVisual.Offset = Vector3.Zero;
        RenderGrid(width, height);
        RenderSeries(primarySamples, primarySegments, width, height, 2);
        RenderSeries(secondarySamples, secondarySegments, width, height, 1.5f);
    }

    private void RenderGrid(float width, float height)
    {
        int lineIndex = 0;

        for (int column = 1; column < GridColumnCount; column++)
        {
            float x = width * column / GridColumnCount;
            SetLine(gridLines[lineIndex++], new Vector2(x, 0), new Vector2(x, height), 1);
        }

        for (int row = 1; row < GridRowCount; row++)
        {
            float y = height * row / GridRowCount;
            SetLine(gridLines[lineIndex++], new Vector2(0, y), new Vector2(width, y), 1);
        }
    }

    private void RenderSeries(double[] samples, IReadOnlyList<SpriteVisual> segments, float width, float height, float thickness)
    {
        double maximum = Maximum > 0 ? Maximum : dynamicMaximum;
        float horizontalStep = width / (SampleCapacity - 1);

        for (int index = 0; index < SampleCapacity - 1; index++)
        {
            Vector2 start = new(index * horizontalStep, GetY(samples[index], maximum, height));
            Vector2 end = new((index + 1) * horizontalStep, GetY(samples[index + 1], maximum, height));
            SetLine(segments[index], start, end, thickness);
            segments[index].IsVisible = true;
        }

        segments[^1].IsVisible = false;
    }

    private void RenderNextSample(double[] samples, double nextSample, IReadOnlyList<SpriteVisual> segments, ContainerVisual? visual, float thickness)
    {
        if (compositor is null || visual is null)
        {
            return;
        }

        float width = (float)ActualWidth;
        float height = (float)ActualHeight;
        float horizontalStep = width / (SampleCapacity - 1);
        double maximum = Maximum > 0 ? Maximum : dynamicMaximum;

        visual.StopAnimation(nameof(visual.Offset));
        visual.Offset = Vector3.Zero;

        for (int index = 0; index < SampleCapacity; index++)
        {
            double endValue = index == SampleCapacity - 1 ? nextSample : samples[index + 1];
            Vector2 start = new(index * horizontalStep, GetY(samples[index], maximum, height));
            Vector2 end = new((index + 1) * horizontalStep, GetY(endValue, maximum, height));
            SetLine(segments[index], start, end, thickness);
            segments[index].IsVisible = true;
        }

        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0), new Vector2(0, 1));
        Vector3KeyFrameAnimation animation = compositor.CreateVector3KeyFrameAnimation();
        animation.Duration = TransitionDuration;
        animation.InsertKeyFrame(1, new Vector3(-horizontalStep, 0, 0), easing);
        visual.StartAnimation(nameof(visual.Offset), animation);
    }

    private static void SetLine(SpriteVisual visual, Vector2 start, Vector2 end, float thickness)
    {
        Vector2 delta = end - start;
        float length = Math.Max(1, delta.Length());
        float angle = MathF.Atan2(delta.Y, delta.X);
        Vector3 offset = new(start.X, start.Y - thickness / 2, 0);
        Vector2 size = new(length + 1, thickness);
        visual.CenterPoint = new Vector3(0, thickness / 2, 0);
        visual.RotationAngle = angle;
        visual.Offset = offset;
        visual.Size = size;
    }

    private static double GetDynamicMaximum(double value)
    {
        double maximum = 1024;

        while (maximum < value * 1.1)
        {
            maximum *= 2;
        }

        return maximum;
    }

    private static float GetY(double value, double maximum, float height)
    {
        double normalized = Math.Clamp(value / Math.Max(1, maximum), 0, 1);
        return height - (float)(normalized * Math.Max(0, height - 2)) - 1;
    }

    private static Windows.UI.Color GetColor(Brush? brush) => brush is SolidColorBrush solidColorBrush ? solidColorBrush.Color : Windows.UI.Color.FromArgb(0, 0, 0, 0);
}
