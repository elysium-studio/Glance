using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;

namespace Glance.Shell.WinUI;

public sealed partial class EmptyModulesView :
    UserControl
{
    private static readonly TimeSpan AnimationDuration = TimeSpan.FromSeconds(3.2);
    private readonly UIElement[] tiles;

    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(EmptyModulesView), new PropertyMetadata(false));

    public EmptyModulesView()
    {
        InitializeComponent();

        tiles =
        [
            CompactTile1,
            CompactTile2,
            CompactTile4,
            CompactTile3,
            ExpandedTile1,
            ExpandedTile2,
            ExpandedTile4,
            ExpandedTile3
        ];

        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
    }

    public event EventHandler? AddModulesRequested;

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public Visibility WhenCompact(bool isExpanded) => isExpanded ? Visibility.Collapsed : Visibility.Visible;

    public Visibility WhenExpanded(bool isExpanded) => isExpanded ? Visibility.Visible : Visibility.Collapsed;

    private void HandleAddModulesRequested(object sender, RoutedEventArgs args) => AddModulesRequested?.Invoke(this, EventArgs.Empty);

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        for (int index = 0; index < tiles.Length; index++)
        {
            StartTileAnimation(tiles[index], index % 4);
        }
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        foreach (UIElement tile in tiles)
        {
            Visual visual = ElementCompositionPreview.GetElementVisual(tile);
            visual.StopAnimation(nameof(visual.Opacity));
            visual.Opacity = 1;
        }
    }

    private static void StartTileAnimation(UIElement tile, int phase)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(tile);
        ScalarKeyFrameAnimation animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        float start = phase * 0.18f;
        animation.Duration = AnimationDuration;
        animation.IterationBehavior = AnimationIterationBehavior.Forever;
        animation.InsertKeyFrame(0, 0.45f);
        animation.InsertKeyFrame(start, 0.45f);
        animation.InsertKeyFrame(Math.Min(start + 0.18f, 0.9f), 1);
        animation.InsertKeyFrame(Math.Min(start + 0.36f, 0.98f), 0.45f);
        animation.InsertKeyFrame(1, 0.45f);
        visual.StartAnimation(nameof(visual.Opacity), animation);
    }
}
