using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT;
using WinRT.Interop;
using WinUIEx;

namespace Glance.ColorPicker.WinUI;

internal sealed partial class CursorColorPreviewWindow :
    IDisposable
{
    private const int CursorOffset = 18;
    private const int ExtendedWindowStyleIndex = -20;
    private const int PreviewSize = 30;
    private const int TransparentWindowStyle = 0x00000020;
    private const int ToolWindowStyle = 0x00000080;
    private const int NoActivateWindowStyle = 0x08000000;

    private readonly Border colorPreview;
    private readonly Window window = new();
    private bool isVisible;

    public CursorColorPreviewWindow()
    {
        colorPreview = new Border
        {
            CornerRadius = new CornerRadius(5)
        };

        Border innerBorder = new()
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(230, 255, 255, 255)),
            Child = colorPreview,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(1)
        };

        Border outerBorder = new()
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 0, 0, 0)),
            Child = innerBorder,
            CornerRadius = new CornerRadius(7),
            IsHitTestVisible = false,
            Padding = new Thickness(1)
        };

        window.Content = outerBorder;
        window.ExtendsContentIntoTitleBar = true;
        window.SetTitleBar(null);
        window.SystemBackdrop = new TransparentTintBackdrop();
        window.AppWindow.IsShownInSwitchers = false;

        OverlappedPresenter presenter = window.AppWindow.Presenter.As<OverlappedPresenter>();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsAlwaysOnTop = true;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsResizable = false;

        nint handle = WindowNative.GetWindowHandle(window);
        int extendedStyle = GetWindowLong(handle, ExtendedWindowStyleIndex);
        _ = SetWindowLong(handle, ExtendedWindowStyleIndex, extendedStyle | TransparentWindowStyle | ToolWindowStyle | NoActivateWindowStyle);
        window.AppWindow.Resize(new SizeInt32(PreviewSize, PreviewSize));
    }

    public void Show(ColorValue color, int cursorX, int cursorY)
    {
        colorPreview.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, color.Red, color.Green, color.Blue));

        PointInt32 cursor = new(cursorX, cursorY);
        RectInt32 bounds = DisplayArea.GetFromPoint(cursor, DisplayAreaFallback.Nearest).OuterBounds;
        int x = cursorX + CursorOffset;
        int y = cursorY + CursorOffset;

        if (x + PreviewSize > bounds.X + bounds.Width)
        {
            x = cursorX - CursorOffset - PreviewSize;
        }

        if (y + PreviewSize > bounds.Y + bounds.Height)
        {
            y = cursorY - CursorOffset - PreviewSize;
        }

        window.AppWindow.Move(new PointInt32(x, y));

        if (!isVisible)
        {
            window.AppWindow.Show(activateWindow: false);
            isVisible = true;
        }
    }

    public void Hide()
    {
        if (!isVisible)
        {
            return;
        }

        window.AppWindow.Hide();
        isVisible = false;
    }

    public void Dispose()
    {
        Hide();
        window.Close();
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static partial int GetWindowLong(nint window, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static partial int SetWindowLong(nint window, int index, int value);
}
