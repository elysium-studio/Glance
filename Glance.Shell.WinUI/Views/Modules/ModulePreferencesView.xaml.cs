using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Glance.Shell.WinUI;

public sealed partial class ModulePreferencesView :
    UserControl
{
    private const double AutoScrollEdgeSize = 72;
    private const double MinimumAutoScrollStep = 3;
    private const double MaximumAutoScrollStep = 18;
    private readonly DispatcherTimer autoScrollTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private ScrollViewer? settingsScrollViewer;
    private double autoScrollStep;

    public ModulePreferencesView()
    {
        InitializeComponent();
        autoScrollTimer.Tick += HandleAutoScrollTick;
        Unloaded += HandleUnloaded;
    }

    public ModulePreferencesViewModel ViewModel =>
        (ModulePreferencesViewModel)DataContext;

    private void HandleDragOver(object sender, DragEventArgs args)
    {
        settingsScrollViewer ??= FindAncestor<ScrollViewer>(this);

        if (settingsScrollViewer is null)
        {
            return;
        }

        Point position = args.GetPosition(settingsScrollViewer);
        double viewportHeight = settingsScrollViewer.ViewportHeight;

        if (position.Y < AutoScrollEdgeSize)
        {
            SetAutoScrollStep(-GetAutoScrollStep(AutoScrollEdgeSize - position.Y));
        }
        else if (position.Y > viewportHeight - AutoScrollEdgeSize)
        {
            SetAutoScrollStep(GetAutoScrollStep(position.Y - (viewportHeight - AutoScrollEdgeSize)));
        }
        else
        {
            StopAutoScroll();
        }
    }

    private void HandleDragLeave(object sender, DragEventArgs args) => StopAutoScroll();

    private async void HandleDragItemsCompleted(
        ListViewBase sender,
        DragItemsCompletedEventArgs args)
    {
        StopAutoScroll();
        await ViewModel.SaveOrderAsync();
    }

    private void HandleAutoScrollTick(object? sender, object args)
    {
        if (settingsScrollViewer is null)
        {
            StopAutoScroll();
            return;
        }

        double offset = Math.Clamp(settingsScrollViewer.VerticalOffset + autoScrollStep, 0, settingsScrollViewer.ScrollableHeight);

        if (Math.Abs(offset - settingsScrollViewer.VerticalOffset) < 0.1)
        {
            StopAutoScroll();
            return;
        }

        settingsScrollViewer.ChangeView(null, offset, null, true);
    }

    private void SetAutoScrollStep(double step)
    {
        autoScrollStep = step;

        if (!autoScrollTimer.IsEnabled)
        {
            autoScrollTimer.Start();
        }
    }

    private void StopAutoScroll()
    {
        autoScrollStep = 0;
        autoScrollTimer.Stop();
    }

    private static double GetAutoScrollStep(double edgeDepth)
    {
        double progress = Math.Clamp(edgeDepth / AutoScrollEdgeSize, 0, 1);
        return MinimumAutoScrollStep + ((MaximumAutoScrollStep - MinimumAutoScrollStep) * progress);
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        StopAutoScroll();
        settingsScrollViewer = null;
    }

    private static T? FindAncestor<T>(DependencyObject element) where T : DependencyObject
    {
        DependencyObject? ancestor = VisualTreeHelper.GetParent(element);

        while (ancestor is not null)
        {
            if (ancestor is T match)
            {
                return match;
            }

            ancestor = VisualTreeHelper.GetParent(ancestor);
        }

        return null;
    }
}
