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
    private readonly double[] primarySamples = new double[SampleCapacity];
    private readonly double[] secondarySamples = new double[SampleCapacity];
    private readonly List<CompositionLineGeometry> primaryLines = [];
    private readonly List<CompositionLineGeometry> secondaryLines = [];
    private readonly List<CompositionLineGeometry> gridLines = [];
    private readonly List<CompositionSpriteShape> primaryShapes = [];
    private readonly List<CompositionSpriteShape> secondaryShapes = [];
    private readonly List<CompositionSpriteShape> gridShapes = [];
    private ContainerVisual? rootVisual;
    private ShapeVisual? primaryVisual;
    private ShapeVisual? secondaryVisual;
    private ShapeVisual? gridVisual;
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

    public TimeSpan SampleInterval { get; set; } = TimeSpan.FromSeconds(1);

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
            RenderNextSample(primarySamples, primaryValue, primaryLines, primaryVisual);
            RenderNextSample(secondarySamples, secondaryValue, secondaryLines, secondaryVisual);
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
        gridVisual = compositor.CreateShapeVisual();
        primaryVisual = compositor.CreateShapeVisual();
        secondaryVisual = compositor.CreateShapeVisual();
        rootVisual.Children.InsertAtBottom(gridVisual);
        rootVisual.Children.InsertAtTop(primaryVisual);
        rootVisual.Children.InsertAtTop(secondaryVisual);
        ElementCompositionPreview.SetElementChildVisual(this, rootVisual);

        for (int index = 0; index < GridColumnCount - 1 + GridRowCount - 1; index++)
        {
            CompositionLineGeometry line = compositor.CreateLineGeometry();
            CompositionSpriteShape shape = compositor.CreateSpriteShape(line);
            shape.StrokeThickness = 1;
            gridLines.Add(line);
            gridShapes.Add(shape);
            gridVisual.Shapes.Add(shape);
        }

        for (int index = 0; index < SampleCapacity; index++)
        {
            CompositionLineGeometry primaryLine = compositor.CreateLineGeometry();
            CompositionSpriteShape primaryShape = compositor.CreateSpriteShape(primaryLine);
            primaryShape.StrokeThickness = 1;
            primaryShape.StrokeStartCap = CompositionStrokeCap.Round;
            primaryShape.StrokeEndCap = CompositionStrokeCap.Round;
            primaryLines.Add(primaryLine);
            primaryShapes.Add(primaryShape);
            primaryVisual.Shapes.Add(primaryShape);

            CompositionLineGeometry secondaryLine = compositor.CreateLineGeometry();
            CompositionSpriteShape secondaryShape = compositor.CreateSpriteShape(secondaryLine);
            secondaryShape.StrokeThickness = 1;
            secondaryShape.StrokeStartCap = CompositionStrokeCap.Round;
            secondaryShape.StrokeEndCap = CompositionStrokeCap.Round;
            secondaryLines.Add(secondaryLine);
            secondaryShapes.Add(secondaryShape);
            secondaryVisual.Shapes.Add(secondaryShape);
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

        foreach (CompositionSpriteShape shape in primaryShapes)
        {
            shape.StrokeBrush = primaryBrush;
        }

        foreach (CompositionSpriteShape shape in secondaryShapes)
        {
            shape.StrokeBrush = secondaryBrush;
        }

        foreach (CompositionSpriteShape shape in gridShapes)
        {
            shape.StrokeBrush = gridBrush;
        }

        if (secondaryVisual is not null)
        {
            secondaryVisual.IsVisible = SecondaryStroke is not null;
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

        Vector2 size = new((float)ActualWidth, (float)ActualHeight);
        rootVisual.Size = size;
        gridVisual!.Size = size;
        primaryVisual!.Size = size;
        secondaryVisual!.Size = size;
        primaryVisual.StopAnimation(nameof(primaryVisual.Offset));
        secondaryVisual.StopAnimation(nameof(secondaryVisual.Offset));
        primaryVisual.Offset = Vector3.Zero;
        secondaryVisual.Offset = Vector3.Zero;
        RenderGrid(size.X, size.Y);
        RenderSeries(primarySamples, primaryLines, size.X, size.Y);
        RenderSeries(secondarySamples, secondaryLines, size.X, size.Y);
    }

    private void RenderGrid(float width, float height)
    {
        int lineIndex = 0;

        for (int column = 1; column < GridColumnCount; column++)
        {
            float x = width * column / GridColumnCount;
            SetLine(gridLines[lineIndex++], new Vector2(x, 0), new Vector2(x, height));
        }

        for (int row = 1; row < GridRowCount; row++)
        {
            float y = height * row / GridRowCount;
            SetLine(gridLines[lineIndex++], new Vector2(0, y), new Vector2(width, y));
        }
    }

    private void RenderSeries(double[] samples, IReadOnlyList<CompositionLineGeometry> lines, float width, float height)
    {
        double maximum = Maximum > 0 ? Maximum : dynamicMaximum;
        float horizontalStep = width / (SampleCapacity - 1);

        for (int index = 0; index < SampleCapacity - 1; index++)
        {
            Vector2 start = new(index * horizontalStep, GetY(samples[index], maximum, height));
            Vector2 end = new((index + 1) * horizontalStep, GetY(samples[index + 1], maximum, height));
            SetLine(lines[index], start, end);
        }

        SetLine(lines[^1], new Vector2(-2, -2), new Vector2(-2, -2));
    }

    private void RenderNextSample(double[] samples, double nextSample, IReadOnlyList<CompositionLineGeometry> lines, ShapeVisual? visual)
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
            SetLine(lines[index], start, end);
        }

        Vector3KeyFrameAnimation animation = compositor.CreateVector3KeyFrameAnimation();
        animation.Duration = SampleInterval;
        animation.InsertKeyFrame(1, new Vector3(-horizontalStep, 0, 0));
        visual.StartAnimation(nameof(visual.Offset), animation);
    }

    private static void SetLine(CompositionLineGeometry line, Vector2 start, Vector2 end)
    {
        line.Start = start;
        line.End = end;
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
