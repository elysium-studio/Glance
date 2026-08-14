using Microsoft.UI.Xaml;

namespace Glance.UI.WinUI;

public sealed class ElementSizeTrigger : StateTriggerBase
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source),
        typeof(FrameworkElement),
        typeof(ElementSizeTrigger),
        new PropertyMetadata(null, HandleSourceChanged));

    public static readonly DependencyProperty MinWidthProperty = DependencyProperty.Register(nameof(MinWidth),
        typeof(double),
        typeof(ElementSizeTrigger),
        new PropertyMetadata(double.NaN, HandleConstraintChanged));

    public static readonly DependencyProperty MaxWidthProperty = DependencyProperty.Register(nameof(MaxWidth),
        typeof(double),
        typeof(ElementSizeTrigger),
        new PropertyMetadata(double.NaN, HandleConstraintChanged));

    public static readonly DependencyProperty MinHeightProperty = DependencyProperty.Register(nameof(MinHeight),
        typeof(double),
        typeof(ElementSizeTrigger),
        new PropertyMetadata(double.NaN, HandleConstraintChanged));

    public static readonly DependencyProperty MaxHeightProperty = DependencyProperty.Register(nameof(MaxHeight),
        typeof(double),
        typeof(ElementSizeTrigger),
        new PropertyMetadata(double.NaN, HandleConstraintChanged));

    public FrameworkElement? Source
    {
        get => (FrameworkElement?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public double MinWidth
    {
        get => (double)GetValue(MinWidthProperty);
        set => SetValue(MinWidthProperty, value);
    }

    public double MaxWidth
    {
        get => (double)GetValue(MaxWidthProperty);
        set => SetValue(MaxWidthProperty, value);
    }

    public double MinHeight
    {
        get => (double)GetValue(MinHeightProperty);
        set => SetValue(MinHeightProperty, value);
    }

    public double MaxHeight
    {
        get => (double)GetValue(MaxHeightProperty);
        set => SetValue(MaxHeightProperty, value);
    }

    private static void HandleSourceChanged(DependencyObject sender,
        DependencyPropertyChangedEventArgs args)
    {
        ElementSizeTrigger trigger = (ElementSizeTrigger)sender;

        if (args.OldValue is FrameworkElement previous)
        {
            previous.SizeChanged -= trigger.HandleSizeChanged;
        }

        if (args.NewValue is FrameworkElement current)
        {
            current.SizeChanged += trigger.HandleSizeChanged;
        }

        trigger.Update();
    }

    private static void HandleConstraintChanged(DependencyObject sender,
        DependencyPropertyChangedEventArgs args) => ((ElementSizeTrigger)sender).Update();

    private void HandleSizeChanged(object sender,
        SizeChangedEventArgs args) => Update();

    private void Update()
    {
        FrameworkElement? source = Source;

        if (source is null || source.ActualWidth <= 0 || source.ActualHeight <= 0)
        {
            SetActive(false);
            return;
        }

        SetActive(IsWithin(source.ActualWidth, MinWidth, MaxWidth) &&
            IsWithin(source.ActualHeight, MinHeight, MaxHeight));
    }

    private static bool IsWithin(double value,
        double minimum,
        double maximum) => (double.IsNaN(minimum) || value >= minimum) &&
            (double.IsNaN(maximum) || value <= maximum);
}
