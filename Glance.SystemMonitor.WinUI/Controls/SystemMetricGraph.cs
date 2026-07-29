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
    private static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(220);
    private readonly double[] primarySamples = new double[SampleCapacity];
    private readonly double[] secondarySamples = new double[SampleCapacity];
    private readonly List<SpriteVisual> primarySegments = [];
    private readonly List<SpriteVisual> secondarySegments = [];
    private readonly List<SpriteVisual> gridLines = [];
    private ContainerVisual? rootVisual;
    private CompositionColorBrush? primaryBrush;
    private CompositionColorBrush? secondaryBrush;
    private CompositionColorBrush? gridBrush;
    private Compositor? compositor;
    private double dynamicMaximum = 1024;
    private bool hasRendered;

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
        ShiftSamples(primarySamples, Math.Max(0, primary));
        ShiftSamples(secondarySamples, Math.Max(0, secondary ?? 0));

        if (Maximum <= 0)
        {
            double currentMaximum = Math.Max(primarySamples.Max(), secondarySamples.Max());
            dynamicMaximum = Math.Max(1024, Math.Max(currentMaximum * 1.1, dynamicMaximum * 0.92));
        }

        Render(true);
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
            graph.Render(false);
        }
    }

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        EnsureVisuals();
        Render(false);
    }

    private void HandleSizeChanged(object sender, SizeChangedEventArgs args) => Render(false);

    private void HandleActualThemeChanged(FrameworkElement sender, object args)
    {
        UpdateBrushes();
        Render(false);
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
        ElementCompositionPreview.SetElementChildVisual(this, rootVisual);

        for (int index = 0; index < GridColumnCount - 1 + GridRowCount - 1; index++)
        {
            SpriteVisual line = compositor.CreateSpriteVisual();
            gridLines.Add(line);
            rootVisual.Children.InsertAtBottom(line);
        }

        for (int index = 0; index < SampleCapacity - 1; index++)
        {
            SpriteVisual primarySegment = compositor.CreateSpriteVisual();
            primarySegments.Add(primarySegment);
            rootVisual.Children.InsertAtTop(primarySegment);

            SpriteVisual secondarySegment = compositor.CreateSpriteVisual();
            secondarySegments.Add(secondarySegment);
            rootVisual.Children.InsertAtTop(secondarySegment);
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

    private void Render(bool animate)
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
        RenderGrid(width, height);
        RenderSeries(primarySamples, primarySegments, width, height, 2, animate && hasRendered);
        RenderSeries(secondarySamples, secondarySegments, width, height, 1.5f, animate && hasRendered);
        hasRendered = true;
    }

    private void RenderGrid(float width, float height)
    {
        int lineIndex = 0;

        for (int column = 1; column < GridColumnCount; column++)
        {
            float x = width * column / GridColumnCount;
            SetLine(gridLines[lineIndex++], new Vector2(x, 0), new Vector2(x, height), 1, false);
        }

        for (int row = 1; row < GridRowCount; row++)
        {
            float y = height * row / GridRowCount;
            SetLine(gridLines[lineIndex++], new Vector2(0, y), new Vector2(width, y), 1, false);
        }
    }

    private void RenderSeries(double[] samples, IReadOnlyList<SpriteVisual> segments, float width, float height, float thickness, bool animate)
    {
        double maximum = Maximum > 0 ? Maximum : dynamicMaximum;
        float horizontalStep = width / (SampleCapacity - 1);

        for (int index = 0; index < segments.Count; index++)
        {
            Vector2 start = new(index * horizontalStep, GetY(samples[index], maximum, height));
            Vector2 end = new((index + 1) * horizontalStep, GetY(samples[index + 1], maximum, height));
            SetLine(segments[index], start, end, thickness, animate);
        }
    }

    private void SetLine(SpriteVisual visual, Vector2 start, Vector2 end, float thickness, bool animate)
    {
        Vector2 delta = end - start;
        float length = Math.Max(1, delta.Length());
        float angle = MathF.Atan2(delta.Y, delta.X);
        Vector3 offset = new(start.X, start.Y - thickness / 2, 0);
        Vector2 size = new(length + 1, thickness);
        visual.CenterPoint = new Vector3(0, thickness / 2, 0);
        visual.RotationAngle = angle;

        if (!animate || compositor is null)
        {
            visual.Offset = offset;
            visual.Size = size;
            return;
        }

        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0), new Vector2(0, 1));
        Vector3KeyFrameAnimation offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
        offsetAnimation.Duration = TransitionDuration;
        offsetAnimation.InsertKeyFrame(1, offset, easing);
        visual.StartAnimation(nameof(visual.Offset), offsetAnimation);

        Vector2KeyFrameAnimation sizeAnimation = compositor.CreateVector2KeyFrameAnimation();
        sizeAnimation.Duration = TransitionDuration;
        sizeAnimation.InsertKeyFrame(1, size, easing);
        visual.StartAnimation(nameof(visual.Size), sizeAnimation);
    }

    private static float GetY(double value, double maximum, float height)
    {
        double normalized = Math.Clamp(value / Math.Max(1, maximum), 0, 1);
        return height - (float)(normalized * Math.Max(0, height - 2)) - 1;
    }

    private static Windows.UI.Color GetColor(Brush? brush) => brush is SolidColorBrush solidColorBrush ? solidColorBrush.Color : Windows.UI.Color.FromArgb(0, 0, 0, 0);
}
