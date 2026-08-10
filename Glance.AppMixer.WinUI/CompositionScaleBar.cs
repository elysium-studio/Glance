using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System.Numerics;

namespace Glance.AppMixer.WinUI;

public sealed partial class CompositionScaleBar :
    UserControl
{
    public static readonly DependencyProperty BarCornerRadiusProperty = DependencyProperty.Register(nameof(BarCornerRadius),
        typeof(CornerRadius),
        typeof(CompositionScaleBar),
        new PropertyMetadata(default(CornerRadius)));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(nameof(Fill),
        typeof(Brush),
        typeof(CompositionScaleBar),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value),
        typeof(double),
        typeof(CompositionScaleBar),
        new PropertyMetadata(0d, HandleValueChanged));

    private ImplicitAnimationCollection? implicitAnimations;
    private Visual? visual;

    public CompositionScaleBar()
    {
        InitializeComponent();
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
    }

    public CornerRadius BarCornerRadius
    {
        get => (CornerRadius)GetValue(BarCornerRadiusProperty);
        set => SetValue(BarCornerRadiusProperty, value);
    }

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static void HandleValueChanged(DependencyObject sender,
        DependencyPropertyChangedEventArgs args) => ((CompositionScaleBar)sender).UpdateScale();

    private void HandleLoaded(object sender,
        RoutedEventArgs args)
    {
        visual = ElementCompositionPreview.GetElementVisual(Bar);
        visual.CenterPoint = Vector3.Zero;
        visual.Scale = CreateScale(Value);

        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.33f, 0),
            new Vector2(0.1f, 1));
        Vector3KeyFrameAnimation animation = compositor.CreateVector3KeyFrameAnimation();
        animation.Target = nameof(Visual.Scale);
        animation.Duration = TimeSpan.FromMilliseconds(100);
        animation.InsertExpressionKeyFrame(1, "this.FinalValue", easing);

        implicitAnimations = compositor.CreateImplicitAnimationCollection();
        implicitAnimations[nameof(Visual.Scale)] = animation;
        visual.ImplicitAnimations = implicitAnimations;
    }

    private void HandleUnloaded(object sender,
        RoutedEventArgs args)
    {
        if (visual is not null)
        {
            visual.ImplicitAnimations = null;
            visual.StopAnimation(nameof(Visual.Scale));
        }

        implicitAnimations?.Dispose();
        implicitAnimations = null;
        visual = null;
    }

    private void UpdateScale()
    {
        if (visual is not null)
        {
            visual.Scale = CreateScale(Value);
        }
    }

    private static Vector3 CreateScale(double value) => new((float)Math.Clamp(value, 0, 1), 1, 1);
}
