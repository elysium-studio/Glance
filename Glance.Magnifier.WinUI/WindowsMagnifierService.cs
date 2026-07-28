using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Glance.Magnifier.WinUI;

internal sealed partial class WindowsMagnifierService :
    IMagnifierService
{
    private readonly bool isInitialized = MagnificationNativeMethods.MagInitialize();
    private CancellationTokenSource? toolbarSuppression;
    private nint nativeToolbar;
    private bool isDisposed;

    public MagnifierState GetState()
    {
        HideNativeToolbar();

        if (!isInitialized ||
            !MagnificationNativeMethods.MagGetFullscreenTransform(out float zoomFactor, out _, out _))
        {
            return new(false, 1);
        }

        return new(true, Math.Max(1, zoomFactor));
    }

    public bool ZoomIn()
    {
        BeginSuppressingNativeToolbar();
        return SendShortcut(VIRTUAL_KEY.VK_ADD);
    }

    public bool ZoomOut()
    {
        BeginSuppressingNativeToolbar();
        return SendShortcut(VIRTUAL_KEY.VK_SUBTRACT);
    }

    public bool Close()
    {
        bool succeeded = SendShortcut(VIRTUAL_KEY.VK_ESCAPE);
        nativeToolbar = nint.Zero;
        return succeeded;
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        toolbarSuppression?.Cancel();
        toolbarSuppression?.Dispose();

        if (isInitialized)
        {
            MagnificationNativeMethods.MagUninitialize();
        }
    }

    private void BeginSuppressingNativeToolbar()
    {
        toolbarSuppression?.Cancel();
        toolbarSuppression?.Dispose();
        toolbarSuppression = new();
        _ = SuppressNativeToolbarAsync(toolbarSuppression.Token);
    }

    private async Task SuppressNativeToolbarAsync(CancellationToken cancellationToken)
    {
        try
        {
            for (int attempt = 0; attempt < 80; attempt++)
            {
                HideNativeToolbar();
                await Task.Delay(25, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        { }
    }

    private void HideNativeToolbar()
    {
        if (nativeToolbar != nint.Zero &&
            MagnificationNativeMethods.IsWindow(nativeToolbar))
        {
            MagnificationNativeMethods.ShowWindow(nativeToolbar, 0);
            return;
        }

        nativeToolbar = nint.Zero;

        foreach (Process process in Process.GetProcessesByName("Magnify"))
        {
            using (process)
            {
                nint window;

                try
                {
                    process.Refresh();
                    window = process.MainWindowHandle;
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                if (window == nint.Zero)
                {
                    continue;
                }

                nativeToolbar = window;
                MagnificationNativeMethods.ShowWindow(window, 0);
                return;
            }
        }
    }

    private static bool SendShortcut(VIRTUAL_KEY key)
    {
        INPUT[] inputs =
        [
            CreateKey(VIRTUAL_KEY.VK_LWIN),
            CreateKey(key),
            CreateKey(key, KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP),
            CreateKey(VIRTUAL_KEY.VK_LWIN, KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP)
        ];

        return PInvoke.SendInput(inputs, Marshal.SizeOf<INPUT>()) == inputs.Length;
    }

    private static INPUT CreateKey(VIRTUAL_KEY key,
        KEYBD_EVENT_FLAGS flags = 0)
    {
        INPUT input = new() { type = INPUT_TYPE.INPUT_KEYBOARD };
        input.Anonymous.ki.wVk = key;
        input.Anonymous.ki.dwFlags = flags;
        return input;
    }
}

internal static partial class MagnificationNativeMethods
{
    [LibraryImport("Magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MagInitialize();

    [LibraryImport("Magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MagUninitialize();

    [LibraryImport("Magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MagGetFullscreenTransform(out float magnificationLevel,
        out int xOffset,
        out int yOffset);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindow(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint window,
        int command);
}
