using Microsoft.UI.Dispatching;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.ColorPicker.WinUI;

public sealed partial class WindowsColorPickerService :
    IColorPickerService,
    IDisposable
{
    private const int EscapeKey = 0x1B;
    private const int LeftMouseButton = 0x01;
    private readonly DispatcherQueue dispatcherQueue;
    private CancellationTokenSource? cancellationTokenSource;
    private CursorColorPreviewWindow? cursorPreviewWindow;
    private bool isPicking;

    public WindowsColorPickerService() =>
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

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
        cursorPreviewWindow ??= new CursorColorPreviewWindow();
        cancellationTokenSource = new CancellationTokenSource();
        _ = TrackPointerAsync(cancellationTokenSource.Token);
    }

    public void CancelPicking()
    {
        if (!isPicking)
        {
            return;
        }

        cancellationTokenSource?.Cancel();
        CompletePicking(null);
    }

    public void Dispose()
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        cursorPreviewWindow?.Dispose();
    }

    private async Task TrackPointerAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (IsKeyPressed(LeftMouseButton) && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(20, cancellationToken).ConfigureAwait(false);
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                if (IsKeyPressed(EscapeKey))
                {
                    CompletePicking(null);
                    return;
                }

                ColorSample? sample = ReadColorUnderPointer();

                if (sample is ColorSample preview)
                {
                    dispatcherQueue.TryEnqueue(() =>
                    {
                        cursorPreviewWindow?.Show(preview.Color, preview.X, preview.Y);
                        PreviewChanged?.Invoke(this, new ColorPickerEventArgs(preview.Color));
                    });
                }

                if (IsKeyPressed(LeftMouseButton))
                {
                    CompletePicking(sample?.Color);
                    return;
                }

                await Task.Delay(33, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CompletePicking(ColorValue? color)
    {
        if (!isPicking)
        {
            return;
        }

        isPicking = false;
        cancellationTokenSource?.Cancel();

        dispatcherQueue.TryEnqueue(() =>
        {
            cursorPreviewWindow?.Hide();

            if (color is ColorValue pickedColor)
            {
                ColorPicked?.Invoke(this, new ColorPickerEventArgs(pickedColor));
            }
            else
            {
                PickingCancelled?.Invoke(this, EventArgs.Empty);
            }
        });
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
