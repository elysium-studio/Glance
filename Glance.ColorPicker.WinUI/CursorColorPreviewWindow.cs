using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Graphics;
using WinRT;
using WinRT.Interop;
using WinUIEx;

namespace Glance.ColorPicker.WinUI;

internal sealed partial class CursorColorPreviewWindow :
    IDisposable
{
    private const int CaptionStyle = 0x00C00000;
    private const int CursorOffset = 16;
    private const uint DwmBorderColorAttribute = 34;
    private const uint DwmColorNone = 0xFFFFFFFE;
    private const uint DwmCornerPreferenceAttribute = 33;
    private const uint DwmDoNotRound = 1;
    private const int ExtendedWindowStyleIndex = -20;
    private const int NoActivateWindowStyle = 0x08000000;
    private const int PreviewSize = 26;
    private const int ResizableFrameStyle = 0x00040000;
    private const int SystemMenuStyle = 0x00080000;
    private const int ToolWindowStyle = 0x00000080;
    private const int TransparentWindowStyle = 0x00000020;
    private const int WindowStyleIndex = -16;

    private readonly Border colorPreview;
    private readonly Window window = new();
    private int disposed;
    private bool isVisible;

    public CursorColorPreviewWindow()
    {
        colorPreview = new Border
        {
            CornerRadius = new CornerRadius(6),
            IsHitTestVisible = false
        };

        window.Content = colorPreview;
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
        int style = GetWindowLong(handle, WindowStyleIndex);
        _ = SetWindowLong(handle, WindowStyleIndex, style & ~CaptionStyle & ~ResizableFrameStyle & ~SystemMenuStyle);
        int extendedStyle = GetWindowLong(handle, ExtendedWindowStyleIndex);
        _ = SetWindowLong(handle, ExtendedWindowStyleIndex, extendedStyle | TransparentWindowStyle | ToolWindowStyle | NoActivateWindowStyle);
        uint cornerPreference = DwmDoNotRound;
        uint borderColor = DwmColorNone;
        _ = DwmSetWindowAttribute(handle, DwmCornerPreferenceAttribute, in cornerPreference, sizeof(uint));
        _ = DwmSetWindowAttribute(handle, DwmBorderColorAttribute, in borderColor, sizeof(uint));
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
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        Hide();
        window.Close();
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static partial int GetWindowLong(nint window, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static partial int SetWindowLong(nint window, int index, int value);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint window, uint attribute, in uint value, uint size);
}
