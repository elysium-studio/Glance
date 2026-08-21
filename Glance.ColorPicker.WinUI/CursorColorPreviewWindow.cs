using Microsoft.UI.Windowing;
using Microsoft.UI.Composition.SystemBackdrops;
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
    private const int PreviewHeight = 44;
    private const int PreviewWidth = 116;
    private const int ResizableFrameStyle = 0x00040000;
    private const int SystemMenuStyle = 0x00080000;
    private const int ToolWindowStyle = 0x00000080;
    private const int TransparentWindowStyle = 0x00000020;
    private const int WindowStyleIndex = -16;

    private readonly Border colorPreview;
    private readonly TextBlock colorValue;
    private readonly Window window = new();
    private readonly int windowHeight;
    private readonly int windowWidth;
    private int disposed;
    private bool isVisible;

    public CursorColorPreviewWindow()
    {
        colorPreview = new Border
        {
            BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["ControlStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Height = 34,
            IsHitTestVisible = false,
            Width = 34
        };
        colorValue = new TextBlock
        {
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid content = new()
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0)),
            BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["SurfaceStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            IsHitTestVisible = false,
            Padding = new Thickness(4)
        };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition());
        content.Children.Add(colorPreview);
        Grid.SetColumn(colorValue, 1);
        colorValue.Margin = new Thickness(8, 0, 8, 0);
        content.Children.Add(colorValue);

        window.Content = content;
        window.ExtendsContentIntoTitleBar = true;
        window.SetTitleBar(null);
        window.SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
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
        double scale = GetDpiForWindow(handle) / 96d;
        windowWidth = (int)Math.Round(PreviewWidth * scale);
        windowHeight = (int)Math.Round(PreviewHeight * scale);
        window.AppWindow.Resize(new SizeInt32(windowWidth, windowHeight));
    }

    public void Show(ColorValue color, int cursorX, int cursorY)
    {
        colorPreview.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, color.Red, color.Green, color.Blue));
        colorValue.Text = color.Hex.TrimStart('#');

        PointInt32 cursor = new(cursorX, cursorY);
        RectInt32 bounds = DisplayArea.GetFromPoint(cursor, DisplayAreaFallback.Nearest).OuterBounds;
        int x = cursorX + CursorOffset;
        int y = cursorY + CursorOffset;

        if (x + windowWidth > bounds.X + bounds.Width)
        {
            x = cursorX - CursorOffset - windowWidth;
        }

        if (y + windowHeight > bounds.Y + bounds.Height)
        {
            y = cursorY - CursorOffset - windowHeight;
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

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(nint window);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint window, uint attribute, in uint value, uint size);
}
