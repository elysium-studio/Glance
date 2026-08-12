using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Glance.Network.WinUI;

public sealed partial class NetworkMetricGraph :
    Grid
{
    private const int SampleCapacity = 48;
    private const int GridColumnCount = 6;
    private const int GridRowCount = 3;
    private readonly double[] samples = new double[SampleCapacity];
    private readonly List<CompositionLineGeometry> sampleLines = [];
    private readonly List<CompositionLineGeometry> gridLines = [];
    private readonly List<CompositionSpriteShape> sampleShapes = [];
    private readonly List<CompositionSpriteShape> gridShapes = [];
    private ContainerVisual? rootVisual;
    private ShapeVisual? sampleVisual;
    private ShapeVisual? gridVisual;
    private CompositionColorBrush? sampleBrush;
    private CompositionColorBrush? gridBrush;
    private Compositor? compositor;
    private double dynamicMaximum = 1024;

    public NetworkMetricGraph()
    {
        IsHitTestVisible = false;
        Loaded += HandleLoaded;
        SizeChanged += HandleSizeChanged;
        ActualThemeChanged += HandleActualThemeChanged;
    }

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(NetworkMetricGraph), new PropertyMetadata(100d, HandleAppearanceChanged));

    public static readonly DependencyProperty PrimaryStrokeProperty = DependencyProperty.Register(nameof(PrimaryStroke), typeof(Brush), typeof(NetworkMetricGraph), new PropertyMetadata(null, HandleAppearanceChanged));

    public static readonly DependencyProperty GridStrokeProperty = DependencyProperty.Register(nameof(GridStroke), typeof(Brush), typeof(NetworkMetricGraph), new PropertyMetadata(null, HandleAppearanceChanged));

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

    public Brush GridStroke
    {
        get => (Brush)GetValue(GridStrokeProperty);
        set => SetValue(GridStrokeProperty, value);
    }

    public TimeSpan SampleInterval { get; set; } = TimeSpan.FromSeconds(1);

    public void AddSample(double value)
    {
        double nextValue = Math.Max(0, value);

        if (Maximum <= 0)
        {
            dynamicMaximum = GetDynamicMaximum(Math.Max(samples.Max(), nextValue));
        }

        if (IsLoaded && ActualWidth > 0 && ActualHeight > 0)
        {
            EnsureVisuals();
            RenderNextSample(nextValue);
        }

        Array.Copy(samples, 1, samples, 0, samples.Length - 1);
        samples[^1] = nextValue;
    }

    private static void HandleAppearanceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is NetworkMetricGraph graph)
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
        sampleVisual = compositor.CreateShapeVisual();
        rootVisual.Children.InsertAtBottom(gridVisual);
        rootVisual.Children.InsertAtTop(sampleVisual);
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
            CompositionLineGeometry line = compositor.CreateLineGeometry();
            CompositionSpriteShape shape = compositor.CreateSpriteShape(line);
            shape.StrokeThickness = 1;
            shape.StrokeStartCap = CompositionStrokeCap.Round;
            shape.StrokeEndCap = CompositionStrokeCap.Round;
            sampleLines.Add(line);
            sampleShapes.Add(shape);
            sampleVisual.Shapes.Add(shape);
        }

        UpdateBrushes();
    }

    private void UpdateBrushes()
    {
        if (compositor is null)
        {
            return;
        }

        sampleBrush?.Dispose();
        gridBrush?.Dispose();
        sampleBrush = compositor.CreateColorBrush(GetColor(PrimaryStroke));
        gridBrush = compositor.CreateColorBrush(GetColor(GridStroke));

        foreach (CompositionSpriteShape shape in sampleShapes)
        {
            shape.StrokeBrush = sampleBrush;
        }

        foreach (CompositionSpriteShape shape in gridShapes)
        {
            shape.StrokeBrush = gridBrush;
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
        sampleVisual!.Size = size;
        sampleVisual.StopAnimation(nameof(sampleVisual.Offset));
        sampleVisual.Offset = Vector3.Zero;
        RenderGrid(size.X, size.Y);
        RenderSeries(size.X, size.Y);
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

    private void RenderSeries(float width, float height)
    {
        double maximum = Maximum > 0 ? Maximum : dynamicMaximum;
        float horizontalStep = width / (SampleCapacity - 1);

        for (int index = 0; index < SampleCapacity - 1; index++)
        {
            Vector2 start = new(index * horizontalStep, GetY(samples[index], maximum, height));
            Vector2 end = new((index + 1) * horizontalStep, GetY(samples[index + 1], maximum, height));
            SetLine(sampleLines[index], start, end);
        }

        SetLine(sampleLines[^1], new Vector2(-2, -2), new Vector2(-2, -2));
    }

    private void RenderNextSample(double nextSample)
    {
        if (compositor is null || sampleVisual is null)
        {
            return;
        }

        float width = (float)ActualWidth;
        float height = (float)ActualHeight;
        float horizontalStep = width / (SampleCapacity - 1);
        double maximum = Maximum > 0 ? Maximum : dynamicMaximum;

        sampleVisual.StopAnimation(nameof(sampleVisual.Offset));
        sampleVisual.Offset = Vector3.Zero;

        for (int index = 0; index < SampleCapacity; index++)
        {
            double endValue = index == SampleCapacity - 1 ? nextSample : samples[index + 1];
            Vector2 start = new(index * horizontalStep, GetY(samples[index], maximum, height));
            Vector2 end = new((index + 1) * horizontalStep, GetY(endValue, maximum, height));
            SetLine(sampleLines[index], start, end);
        }

        Vector3KeyFrameAnimation animation = compositor.CreateVector3KeyFrameAnimation();
        animation.Duration = SampleInterval;
        animation.InsertKeyFrame(1, new Vector3(-horizontalStep, 0, 0));
        sampleVisual.StartAnimation(nameof(sampleVisual.Offset), animation);
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
