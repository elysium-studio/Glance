using Glance.Application.Abstractions;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System.Numerics;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Glance.Inspector.WinUI;

internal sealed class InspectionDistributionView :
    UserControl
{
    private readonly SolidColorBrush[] palette = [new(), new(), new(), new(), new(), new(), new()];
    private readonly List<Border> segments = [];
    private readonly UISettings settings = new();
    private bool animated;
    private bool subscribed;

    public InspectionDistributionView(GlanceInspectionDistribution distribution)
    {
        UpdatePalette();
        StackPanel content = new() { Spacing = 10 };
        Grid bar = new() { Height = 12 };
        long total = distribution.Items.Sum(item => Math.Max(0, item.Value));

        for (int index = 0; index < distribution.Items.Count; index++)
        {
            GlanceInspectionDistributionItem item = distribution.Items[index];
            Brush brush = palette[index % palette.Length];
            double weight = total > 0 ? Math.Max(0.001, (double)item.Value / total) : 1;
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(weight, GridUnitType.Star) });
            Border segment = new() { Background = brush };
            Grid.SetColumn(segment, index);
            bar.Children.Add(segment);
            segments.Add(segment);
        }

        Border barSurface = new()
        {
            Background = ResolveBrush("ControlFillColorSecondaryBrush"),
            Child = bar,
            CornerRadius = new CornerRadius(6)
        };
        content.Children.Add(barSurface);

        for (int index = 0; index < distribution.Items.Count; index++)
        {
            GlanceInspectionDistributionItem item = distribution.Items[index];
            double percentage = total > 0 ? item.Value * 100d / total : 0;
            Grid row = new() { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Border marker = new()
            {
                Background = palette[index % palette.Length],
                CornerRadius = new CornerRadius(4),
                Height = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 8
            };
            TextBlock label = new()
            {
                Text = item.Label,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            TextBlock value = new()
            {
                Foreground = ResolveBrush("TextFillColorSecondaryBrush"),
                Text = $"{percentage:0.#}% · {item.DisplayValue}",
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 1);
            Grid.SetColumn(value, 2);
            row.Children.Add(marker);
            row.Children.Add(label);
            row.Children.Add(value);
            content.Children.Add(row);
        }

        Content = content;
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
    }

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        if (!subscribed)
        {
            subscribed = true;
            ActualThemeChanged += HandleActualThemeChanged;
            settings.ColorValuesChanged += HandleColorValuesChanged;
        }

        UpdatePalette();

        if (animated)
        {
            return;
        }

        animated = true;

        for (int index = 0; index < segments.Count; index++)
        {
            Visual visual = ElementCompositionPreview.GetElementVisual(segments[index]);
            Compositor compositor = visual.Compositor;
            Vector3KeyFrameAnimation animation = compositor.CreateVector3KeyFrameAnimation();
            animation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            animation.DelayTime = TimeSpan.FromMilliseconds(index * 35);
            animation.Duration = TimeSpan.FromMilliseconds(320);
            animation.InsertKeyFrame(0, new Vector3(0, 1, 1));
            animation.InsertKeyFrame(1, Vector3.One, compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1)));
            visual.CenterPoint = Vector3.Zero;
            visual.StartAnimation(nameof(visual.Scale), animation);
        }
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        if (!subscribed)
        {
            return;
        }

        subscribed = false;
        ActualThemeChanged -= HandleActualThemeChanged;
        settings.ColorValuesChanged -= HandleColorValuesChanged;
    }

    private void HandleActualThemeChanged(FrameworkElement sender, object args) => UpdatePalette();

    private void HandleColorValuesChanged(UISettings sender, object args)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            UpdatePalette();
        }
        else
        {
            _ = DispatcherQueue.TryEnqueue(UpdatePalette);
        }
    }

    private void UpdatePalette()
    {
        UIColorType[] colors = ActualTheme == ElementTheme.Light
            ? [UIColorType.Accent, UIColorType.AccentDark1, UIColorType.AccentLight1, UIColorType.AccentDark2, UIColorType.AccentLight2, UIColorType.AccentDark3, UIColorType.AccentLight3]
            : [UIColorType.Accent, UIColorType.AccentLight1, UIColorType.AccentDark1, UIColorType.AccentLight2, UIColorType.AccentDark2, UIColorType.AccentLight3, UIColorType.AccentDark3];

        for (int index = 0; index < palette.Length; index++)
        {
            palette[index].Color = settings.GetColorValue(colors[index]);
        }
    }

    private static Brush ResolveBrush(string key) => Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out object value) && value is Brush brush ? brush : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
}
