using Microsoft.UI.Dispatching;
using System;
using System.Runtime.InteropServices;

namespace Glance.ColorPicker.WinUI;

public sealed partial class WindowsColorPickerService :
    IColorPickerService,
    IDisposable
{
    private const int EscapeKey = 0x1B;
    private const int LeftMouseButton = 0x01;
    private readonly DispatcherQueueTimer trackingTimer;
    private CursorColorPreviewWindow? cursorPreviewWindow;
    private bool isPicking;
    private bool isWaitingForMouseRelease;

    public WindowsColorPickerService()
    {
        trackingTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        trackingTimer.Interval = TimeSpan.FromMilliseconds(16);
        trackingTimer.IsRepeating = true;
        trackingTimer.Tick += HandleTrackingTick;
    }

    public event EventHandler<ColorPickerEventArgs>? PreviewChanged;

    public event EventHandler<ColorPickerEventArgs>? ColorPicked;

    public event EventHandler? PickingCancelled;

    public bool IsPicking => isPicking;

    public void StartPicking()
    {
        if (isPicking)
        {
            return;
        }

        isPicking = true;
        isWaitingForMouseRelease = IsKeyPressed(LeftMouseButton);
        cursorPreviewWindow ??= new CursorColorPreviewWindow();
        trackingTimer.Start();
    }

    public void CancelPicking()
    {
        if (!isPicking)
        {
            return;
        }

        CompletePicking(null);
    }

    public void Dispose()
    {
        trackingTimer.Stop();
        trackingTimer.Tick -= HandleTrackingTick;
        cursorPreviewWindow?.Dispose();
    }

    private void HandleTrackingTick(DispatcherQueueTimer sender, object args)
    {
        if (isWaitingForMouseRelease)
        {
            if (IsKeyPressed(LeftMouseButton))
            {
                return;
            }

            isWaitingForMouseRelease = false;
        }

        if (IsKeyPressed(EscapeKey))
        {
            CompletePicking(null);
            return;
        }

        ColorSample? sample = ReadColorUnderPointer();

        if (sample is ColorSample preview)
        {
            cursorPreviewWindow?.Show(preview.Color, preview.X, preview.Y);
            PreviewChanged?.Invoke(this, new ColorPickerEventArgs(preview.Color));
        }

        if (IsKeyPressed(LeftMouseButton))
        {
            CompletePicking(sample?.Color);
        }
    }

    private void CompletePicking(ColorValue? color)
    {
        if (!isPicking)
        {
            return;
        }

        isPicking = false;
        trackingTimer.Stop();
        cursorPreviewWindow?.Hide();

        if (color is ColorValue pickedColor)
        {
            ColorPicked?.Invoke(this, new ColorPickerEventArgs(pickedColor));
        }
        else
        {
            PickingCancelled?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool IsKeyPressed(int key) =>
        (NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0;

    private static ColorSample? ReadColorUnderPointer()
    {
        if (!NativeMethods.GetCursorPos(out NativePoint point))
        {
            return null;
        }

        nint deviceContext = NativeMethods.GetDC(nint.Zero);

        if (deviceContext == nint.Zero)
        {
            return null;
        }

        try
        {
            uint value = NativeMethods.GetPixel(deviceContext, point.X, point.Y);

            if (value == uint.MaxValue)
            {
                return null;
            }

            ColorValue color = new((byte)(value & 0xFF), (byte)((value >> 8) & 0xFF), (byte)((value >> 16) & 0xFF));
            return new ColorSample(color, point.X, point.Y);
        }
        finally
        {
            _ = NativeMethods.ReleaseDC(nint.Zero, deviceContext);
        }
    }

    private readonly record struct ColorSample(ColorValue Color, int X, int Y);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;

        public int Y;
    }

    private static partial class NativeMethods
    {
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetCursorPos(out NativePoint point);

        [LibraryImport("user32.dll")]
        public static partial nint GetDC(nint window);

        [LibraryImport("user32.dll")]
        public static partial int ReleaseDC(nint window, nint deviceContext);

        [LibraryImport("gdi32.dll")]
        public static partial uint GetPixel(nint deviceContext, int x, int y);

        [LibraryImport("user32.dll")]
        public static partial short GetAsyncKeyState(int virtualKey);
    }
}
