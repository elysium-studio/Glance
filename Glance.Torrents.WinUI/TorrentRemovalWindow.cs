using Elysium.Platform.Windows;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using WinRT;
using WinRT.Interop;
using WinUIEx;
using PlatformWindowExtensions = Elysium.Platform.Windows.WindowExtensions;

namespace Glance.Torrents.WinUI;

internal enum TorrentRemovalChoice
{
    Cancel,
    RemoveFromList,
    RemoveAndDeleteData
}

internal sealed class TorrentRemovalWindow
{
    private readonly TaskCompletionSource<TorrentRemovalChoice> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ContentDialog dialog;
    private readonly Grid root;
    private readonly Border smokeLayer;
    private readonly Window window;
    private bool closed;
    private TorrentRemovalChoice selectedChoice = TorrentRemovalChoice.Cancel;

    private TorrentRemovalWindow(ModuleResourceTextLocalizer<TorrentModule> localizer,
        WindowId ownerWindowId)
    {
        TextBlock description = new()
        {
            Width = 600,
            Text = localizer.GetText("RemoveDescription"),
            TextWrapping = TextWrapping.Wrap,
            Style = Microsoft.UI.Xaml.Application.Current.Resources["BodyTextBlockStyle"] as Style
        };
        dialog = new ContentDialog
        {
            Title = localizer.GetText("RemoveTitle"),
            Content = description,
            PrimaryButtonText = localizer.GetText("RemoveListOnly"),
            SecondaryButtonText = localizer.GetText("RemoveAndDelete"),
            CloseButtonText = localizer.GetText("Cancel"),
            DefaultButton = ContentDialogButton.Close
        };
        dialog.Resources["ContentDialogMaxWidth"] = 680d;
        dialog.Resources["ContentDialogSmokeFill"] = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        dialog.PrimaryButtonClick += HandlePrimaryButtonClick;
        dialog.SecondaryButtonClick += HandleSecondaryButtonClick;
        dialog.Closing += HandleDialogClosing;

        smokeLayer = new Border
        {
            Background = ResolveSmokeBrush(),
            IsHitTestVisible = false,
            Opacity = 0
        };
        root = new Grid
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0))
        };
        root.Children.Add(smokeLayer);
        root.Children.Add(dialog);
        root.Loaded += HandleRootLoaded;

        window = new Window
        {
            Content = root,
            ExtendsContentIntoTitleBar = true,
            SystemBackdrop = new TransparentTintBackdrop()
        };
        window.SetTitleBar(null);
        window.Closed += HandleWindowClosed;
        window.AppWindow.IsShownInSwitchers = false;

        OverlappedPresenter presenter = window.AppWindow.Presenter.As<OverlappedPresenter>();
        presenter.IsAlwaysOnTop = true;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsResizable = false;
        presenter.SetBorderAndTitleBar(false, false);

        nint windowHandle = WindowNative.GetWindowHandle(window);
        PlatformWindowExtensions.SetBorderless(windowHandle, true);
        PlatformWindowExtensions.SetCornerRadius(windowHandle, WindowCornerPreference.Sharp);
        PlatformWindowExtensions.SetTopMost(windowHandle, true);

        DisplayArea displayArea = DisplayArea.GetFromWindowId(ownerWindowId,
            DisplayAreaFallback.Primary);
        window.AppWindow.MoveAndResize(displayArea.OuterBounds);
    }

    public static Task<TorrentRemovalChoice> ShowAsync(ModuleResourceTextLocalizer<TorrentModule> localizer,
        WindowId ownerWindowId) => new TorrentRemovalWindow(localizer,
            ownerWindowId).ShowAsync();

    private Task<TorrentRemovalChoice> ShowAsync()
    {
        window.AppWindow.Show(true);
        return completion.Task;
    }

    private async void HandleRootLoaded(object sender,
        RoutedEventArgs args)
    {
        root.Loaded -= HandleRootLoaded;

        try
        {
            AnimateSmoke(1);
            dialog.XamlRoot = root.XamlRoot;
            _ = await dialog.ShowAsync(ContentDialogPlacement.InPlace);
            completion.TrySetResult(selectedChoice);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
        finally
        {
            Close();
        }
    }

    private void HandleDialogClosing(ContentDialog sender,
        ContentDialogClosingEventArgs args) => AnimateSmoke(0);

    private void HandlePrimaryButtonClick(ContentDialog sender,
        ContentDialogButtonClickEventArgs args) => selectedChoice = TorrentRemovalChoice.RemoveFromList;

    private void HandleSecondaryButtonClick(ContentDialog sender,
        ContentDialogButtonClickEventArgs args) => selectedChoice = TorrentRemovalChoice.RemoveAndDeleteData;

    private void HandleWindowClosed(object sender,
        WindowEventArgs args)
    {
        closed = true;
        completion.TrySetResult(TorrentRemovalChoice.Cancel);
    }

    private void AnimateSmoke(double opacity)
    {
        DoubleAnimation animation = new()
        {
            To = opacity,
            Duration = TimeSpan.FromMilliseconds(83)
        };
        Storyboard.SetTarget(animation, smokeLayer);
        Storyboard.SetTargetProperty(animation, nameof(UIElement.Opacity));
        Storyboard storyboard = new();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private void Close()
    {
        if (closed)
        {
            return;
        }

        closed = true;
        dialog.PrimaryButtonClick -= HandlePrimaryButtonClick;
        dialog.SecondaryButtonClick -= HandleSecondaryButtonClick;
        dialog.Closing -= HandleDialogClosing;
        window.Closed -= HandleWindowClosed;
        window.Close();
    }

    private static Brush ResolveSmokeBrush() => Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("SmokeFillColorDefaultBrush", out object value) && value is Brush brush
        ? brush
        : new SolidColorBrush(Windows.UI.Color.FromArgb(77, 0, 0, 0));
}
