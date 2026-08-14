using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;
using Windows.UI;

namespace Glance.Shell.WinUI;

public sealed partial class ModuleLoadingView :
    UserControl
{
    private const float InitialStartPointX = -7.92f;
    private static readonly TimeSpan ShimmerDuration = TimeSpan.FromMilliseconds(1600);
    private readonly FrameworkElement[] shimmerElements;
    private bool isAnimating;
    private bool restartPending;

    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(ModuleLoadingView), new PropertyMetadata(false));

    public ModuleLoadingView()
    {
        InitializeComponent();
        shimmerElements =
        [
            CompactIconPlaceholder,
            CompactTextPlaceholder,
            ExpandedIconPlaceholder,
            ExpandedTitlePlaceholder,
            ExpandedPrimaryPlaceholder,
            ExpandedSubtitlePlaceholder
        ];
        foreach (FrameworkElement element in shimmerElements)
        {
            element.SizeChanged += HandleShimmerElementSizeChanged;
        }
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
        SizeChanged += HandleSizeChanged;
        ActualThemeChanged += HandleActualThemeChanged;
        _ = RegisterPropertyChangedCallback(VisibilityProperty, HandleVisibilityChanged);
    }

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public Visibility WhenCompact(bool isExpanded) => isExpanded
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility WhenExpanded(bool isExpanded) => isExpanded
        ? Visibility.Visible
        : Visibility.Collapsed;

    private void HandleLoaded(object sender,
        RoutedEventArgs args)
    {
        if (Visibility == Visibility.Visible)
        {
            StartAnimations();
        }
    }

    private void HandleSizeChanged(object sender,
        SizeChangedEventArgs args)
    {
        if (!IsLoaded || Visibility != Visibility.Visible || args.NewSize == args.PreviousSize)
        {
            return;
        }

        QueueAnimationRestart();
    }

    private void HandleShimmerElementSizeChanged(object sender,
        SizeChangedEventArgs args)
    {
        if (!IsLoaded || Visibility != Visibility.Visible || args.NewSize == args.PreviousSize)
        {
            return;
        }

        QueueAnimationRestart();
    }

    private void QueueAnimationRestart()
    {
        if (restartPending)
        {
            return;
        }

        restartPending = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            restartPending = false;

            if (!IsLoaded || Visibility != Visibility.Visible)
            {
                return;
            }

            StopAnimations();
            StartAnimations();
        });
    }

    private void HandleActualThemeChanged(FrameworkElement sender,
        object args)
    {
        if (!isAnimating)
        {
            return;
        }

        StopAnimations();
        StartAnimations();
    }

    private void StartAnimations()
    {
        if (isAnimating)
        {
            return;
        }

        isAnimating = true;

        foreach (FrameworkElement element in shimmerElements)
        {
            if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
            {
                continue;
            }

            StartShimmerAnimation(element);
        }
    }

    private static void StartShimmerAnimation(FrameworkElement element)
    {
        float width = (float)element.ActualWidth;
        float height = (float)element.ActualHeight;
        Compositor compositor = ElementCompositionPreview.GetElementVisual(element).Compositor;
        ShapeVisual shimmerVisual = compositor.CreateShapeVisual();
        shimmerVisual.Size = new Vector2(width, height);

        CompositionRoundedRectangleGeometry geometry = compositor.CreateRoundedRectangleGeometry();
        geometry.Size = shimmerVisual.Size;
        geometry.CornerRadius = new Vector2(4);

        (Color edge, Color center) = GetShimmerColors(element.ActualTheme);
        CompositionLinearGradientBrush shimmerBrush = compositor.CreateLinearGradientBrush();
        shimmerBrush.StartPoint = new Vector2(InitialStartPointX, 0);
        shimmerBrush.EndPoint = new Vector2(0, 1);
        shimmerBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.273f, edge));
        shimmerBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.436f, center));
        shimmerBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.482f, center));
        shimmerBrush.ColorStops.Add(compositor.CreateColorGradientStop(0.643f, edge));

        CompositionSpriteShape shimmerShape = compositor.CreateSpriteShape(geometry);
        shimmerShape.FillBrush = shimmerBrush;
        shimmerVisual.Shapes.Add(shimmerShape);
        ElementCompositionPreview.SetElementChildVisual(element, shimmerVisual);

        Vector2KeyFrameAnimation startPoint = compositor.CreateVector2KeyFrameAnimation();
        startPoint.Duration = ShimmerDuration;
        startPoint.IterationBehavior = AnimationIterationBehavior.Forever;
        startPoint.InsertKeyFrame(0, new Vector2(InitialStartPointX, 0));
        startPoint.InsertKeyFrame(1, Vector2.Zero);

        Vector2KeyFrameAnimation endPoint = compositor.CreateVector2KeyFrameAnimation();
        endPoint.Duration = ShimmerDuration;
        endPoint.IterationBehavior = AnimationIterationBehavior.Forever;
        endPoint.InsertKeyFrame(0, new Vector2(1, 0));
        endPoint.InsertKeyFrame(1, new Vector2(-InitialStartPointX, 1));

        shimmerBrush.StartAnimation(nameof(shimmerBrush.StartPoint), startPoint);
        shimmerBrush.StartAnimation(nameof(shimmerBrush.EndPoint), endPoint);
    }

    private static (Color Edge, Color Center) GetShimmerColors(ElementTheme theme) => theme == ElementTheme.Light
        ? (Color.FromArgb(13, 0, 0, 0), Color.FromArgb(7, 0, 0, 0))
        : (Color.FromArgb(15, 255, 255, 255), Color.FromArgb(8, 255, 255, 255));

    private void HandleUnloaded(object sender,
        RoutedEventArgs args) => StopAnimations();

    private void HandleVisibilityChanged(DependencyObject sender,
        DependencyProperty property)
    {
        if (!IsLoaded)
        {
            return;
        }

        if (Visibility == Visibility.Visible)
        {
            StartAnimations();
        }
        else
        {
            StopAnimations();
        }
    }

    private void StopAnimations()
    {
        if (!isAnimating)
        {
            return;
        }

        isAnimating = false;

        foreach (FrameworkElement element in shimmerElements)
        {
            ElementCompositionPreview.SetElementChildVisual(element, null);
        }
    }
}
