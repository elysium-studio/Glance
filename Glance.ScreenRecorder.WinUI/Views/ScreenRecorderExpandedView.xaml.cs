using Elysium.UI.Controls.WinUI;
using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using Windows.Foundation;

namespace Glance.ScreenRecorder.WinUI;

public sealed partial class ScreenRecorderExpandedView :
    UserControl
{
    private readonly CompositionActivityPulse activityPulse;
    private DesktopIsland? menuExpansionIsland;
    private bool recordingMenuCommandInvoked;

    public ScreenRecorderExpandedView(ScreenRecorderViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        activityPulse = new(this, PulseRing, viewModel, nameof(ScreenRecorderViewModel.IsRecording), () => viewModel.IsRecording);
    }

    public ScreenRecorderViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    internal bool TryGetRecordingLandingBounds(out NativeRectangle bounds)
    {
        bounds = default;
        DesktopIsland? island = FindIsland();

        if (island is null || !RecordingLandingTarget.IsLoaded || RecordingLandingTarget.ActualWidth <= 0 ||
            RecordingLandingTarget.ActualHeight <= 0 || !GetWindowRect(island.Handle, out NativeRect windowBounds))
        {
            return false;
        }

        Rect localBounds = RecordingLandingTarget.TransformToVisual(island).TransformBounds(new Rect(0,
            0,
            RecordingLandingTarget.ActualWidth,
            RecordingLandingTarget.ActualHeight));
        double scale = XamlRoot?.RasterizationScale ?? 1;
        bounds = new NativeRectangle(windowBounds.Left + (int)Math.Round(localBounds.X * scale),
            windowBounds.Top + (int)Math.Round(localBounds.Y * scale),
            windowBounds.Left + (int)Math.Round(localBounds.Right * scale),
            windowBounds.Top + (int)Math.Round(localBounds.Bottom * scale));
        return true;
    }

    public Visibility WhenEmpty(bool hasRecordings, bool isRecording) => !hasRecordings && !isRecording ? Visibility.Visible : Visibility.Collapsed;

    public bool WhenCanStart(bool isBusy, bool isRecording) => !isBusy && !isRecording;

    public Visibility WhenNotRecording(bool isRecording) => isRecording ? Visibility.Collapsed : Visibility.Visible;

    public Visibility WhenRecording(bool isRecording) => isRecording ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WhenPopulated(bool hasRecordings, bool isRecording) => hasRecordings && !isRecording ? Visibility.Visible : Visibility.Collapsed;

    public string ToUpper(string value) => value.ToUpperInvariant();

    private void HandleRecordingMenuOpened(object sender, object args)
    {
        recordingMenuCommandInvoked = false;
        SetMenuExpansionLocked(true);
    }

    private void HandleRecordingMenuClosed(object sender, object args)
    {
        if (recordingMenuCommandInvoked || ViewModel.IsBusy || ViewModel.IsRecording)
        {
            menuExpansionIsland = null;
            return;
        }

        ReleaseMenuExpansionLock();
    }

    private void SetMenuExpansionLocked(bool isLocked)
    {
        DesktopIsland? island = FindIsland();

        if (island is null)
        {
            return;
        }

        DetachMenuExpansionIsland();
        island.IsExpansionLocked = isLocked;
        menuExpansionIsland = isLocked ? island : null;
    }

    private void ReleaseMenuExpansionLock()
    {
        DesktopIsland? island = menuExpansionIsland ?? FindIsland();

        if (island is null)
        {
            return;
        }

        if (island.IsPointerWithinInteractiveRegion)
        {
            menuExpansionIsland = island;
            island.PointerExited -= HandleMenuExpansionIslandPointerExited;
            island.PointerExited += HandleMenuExpansionIslandPointerExited;
            return;
        }

        DetachMenuExpansionIsland();
        island.IsExpansionLocked = false;
    }

    private void HandleMenuExpansionIslandPointerExited(object sender, PointerRoutedEventArgs args)
    {
        DesktopIsland island = (DesktopIsland)sender;
        DetachMenuExpansionIsland();
        island.IsExpansionLocked = false;
    }

    private void DetachMenuExpansionIsland()
    {
        menuExpansionIsland?.PointerExited -= HandleMenuExpansionIslandPointerExited;
        menuExpansionIsland = null;
    }

    private void HandleRecordRegion(object sender, RoutedEventArgs args)
    {
        recordingMenuCommandInvoked = true;
        ViewModel.RecordRegion();
    }

    private void HandleRecordWindow(object sender, RoutedEventArgs args)
    {
        recordingMenuCommandInvoked = true;
        ViewModel.RecordWindow();
    }

    private void HandleRecordDisplay(object sender, RoutedEventArgs args)
    {
        recordingMenuCommandInvoked = true;
        ViewModel.RecordDisplay();
    }

    private DesktopIsland? FindIsland()
    {
        DependencyObject? current = this;

        while (current is not null)
        {
            if (current is DesktopIsland island)
            {
                return island;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect bounds);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
