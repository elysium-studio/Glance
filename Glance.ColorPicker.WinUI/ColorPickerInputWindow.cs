using Elysium.Platform.Windows;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.UI;
using WinRT;
using WinRT.Interop;
using WinUIEx;
using PlatformWindowExtensions = Elysium.Platform.Windows.WindowExtensions;

namespace Glance.ColorPicker.WinUI;

internal sealed partial class ColorPickerInputWindow :
    IDisposable
{
    private const int ExtendedWindowStyleIndex = -20;
    private const int NoActivateWindowStyle = 0x08000000;
    private const int VirtualScreenHeight = 79;
    private const int VirtualScreenLeft = 76;
    private const int VirtualScreenTop = 77;
    private const int VirtualScreenWidth = 78;

    private readonly PickerInputSurface inputSurface = new();
    private readonly Window window;
    private bool isPointerPressed;
    private bool isVisible;

    public ColorPickerInputWindow()
    {
        inputSurface.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        inputSurface.PointerPressed += HandlePointerPressed;
        inputSurface.PointerReleased += HandlePointerReleased;

        window = new Window
        {
            Content = inputSurface,
            ExtendsContentIntoTitleBar = true,
            SystemBackdrop = new TransparentTintBackdrop()
        };

        window.SetTitleBar(null);
        window.AppWindow.IsShownInSwitchers = false;

        OverlappedPresenter presenter = window.AppWindow.Presenter.As<OverlappedPresenter>();
        presenter.IsAlwaysOnTop = true;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsResizable = false;
        presenter.SetBorderAndTitleBar(false, false);

        nint handle = WindowNative.GetWindowHandle(window);
        int extendedStyle = GetWindowLong(handle, ExtendedWindowStyleIndex);
        _ = SetWindowLong(handle, ExtendedWindowStyleIndex, extendedStyle | NoActivateWindowStyle);
        PlatformWindowExtensions.SetBorderless(handle, true);
        PlatformWindowExtensions.SetCornerRadius(handle, WindowCornerPreference.Sharp);
        PlatformWindowExtensions.SetTopMost(handle, true);
    }

    public event EventHandler? Picked;

    public void Show()
    {
        if (isVisible)
        {
            return;
        }

        int x = GetSystemMetrics(VirtualScreenLeft);
        int y = GetSystemMetrics(VirtualScreenTop);
        int width = GetSystemMetrics(VirtualScreenWidth);
        int height = GetSystemMetrics(VirtualScreenHeight);
        window.AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
        window.AppWindow.Show(false);
        isVisible = true;
    }

    public void Hide()
    {
        if (!isVisible)
        {
            return;
        }

        isPointerPressed = false;
        window.AppWindow.Hide();
        isVisible = false;
    }

    public void Dispose()
    {
        Hide();
        inputSurface.PointerPressed -= HandlePointerPressed;
        inputSurface.PointerReleased -= HandlePointerReleased;
        inputSurface.Dispose();
        window.Close();
    }

    private void HandlePointerPressed(object sender, PointerRoutedEventArgs args)
    {
        PointerPoint point = args.GetCurrentPoint(inputSurface);

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        isPointerPressed = true;
        inputSurface.CapturePointer(args.Pointer);
        args.Handled = true;
    }

    private void HandlePointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (!isPointerPressed)
        {
            return;
        }

        isPointerPressed = false;
        inputSurface.ReleasePointerCapture(args.Pointer);
        args.Handled = true;
        Picked?.Invoke(this, EventArgs.Empty);
    }

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static partial int GetWindowLong(nint window, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static partial int SetWindowLong(nint window, int index, int value);

    private sealed class PickerInputSurface :
        Grid,
        IDisposable
    {
        private readonly InputCursor cursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);

        public PickerInputSurface() =>
            ProtectedCursor = cursor;

        public void Dispose()
        {
            ProtectedCursor = null;
            cursor.Dispose();
        }
    }
}
