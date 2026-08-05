using Elysium.Platform.Windows;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT;
using WinRT.Interop;
using WinUIEx;
using PlatformWindowExtensions = Elysium.Platform.Windows.WindowExtensions;

namespace Glance.SpeechToText.WinUI;

internal sealed partial class SpeechModelConsentWindow
{
    private readonly TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ContentDialog dialog;
    private readonly DisplayArea displayArea;
    private readonly Grid root;
    private readonly Window window;
    private bool isClosed;

    private SpeechModelConsentWindow(string title,
        string message,
        string downloadLabel,
        string cancelLabel,
        WindowId ownerWindowId)
    {
        displayArea = DisplayArea.GetFromWindowId(ownerWindowId, DisplayAreaFallback.Primary);
        dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = downloadLabel,
            CloseButtonText = cancelLabel,
            DefaultButton = ContentDialogButton.Primary
        };
        root = new Grid { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)) };
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
    }

    public static Task<bool> ShowAsync(string title,
        string message,
        string downloadLabel,
        string cancelLabel,
        WindowId ownerWindowId)
    {
        SpeechModelConsentWindow consentWindow = new(title, message, downloadLabel, cancelLabel, ownerWindowId);
        return consentWindow.ShowAsync();
    }

    private Task<bool> ShowAsync()
    {
        AppWindow appWindow = window.AppWindow;
        OverlappedPresenter presenter = appWindow.Presenter.As<OverlappedPresenter>();
        presenter.IsAlwaysOnTop = true;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsResizable = false;
        presenter.SetBorderAndTitleBar(false, false);

        nint windowHandle = WindowNative.GetWindowHandle(window);
        PlatformWindowExtensions.SetBorderless(windowHandle, true);
        PlatformWindowExtensions.SetCornerRadius(windowHandle, WindowCornerPreference.Sharp);
        PlatformWindowExtensions.SetTopMost(windowHandle, true);

        appWindow.MoveAndResize(displayArea.OuterBounds);
        appWindow.Show(activateWindow: true);
        return completion.Task;
    }

    private async void HandleRootLoaded(object sender, RoutedEventArgs args)
    {
        root.Loaded -= HandleRootLoaded;

        try
        {
            dialog.XamlRoot = root.XamlRoot;
            ContentDialogResult result = await dialog.ShowAsync();
            _ = completion.TrySetResult(result == ContentDialogResult.Primary);
        }
        catch (Exception exception)
        {
            _ = completion.TrySetException(exception);
        }
        finally
        {
            Close();
        }
    }

    private void HandleWindowClosed(object sender, WindowEventArgs args)
    {
        isClosed = true;
        _ = completion.TrySetResult(false);
    }

    private void Close()
    {
        if (isClosed)
        {
            return;
        }

        isClosed = true;
        window.Closed -= HandleWindowClosed;
        window.Close();
    }
}
