using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Glance.Magnifier.WinUI;

internal sealed partial class WindowsMagnifierService :
    IMagnifierService
{
    private readonly bool isInitialized = MagnificationNativeMethods.MagInitialize();
    private bool isDisposed;

    public MagnifierState GetState()
    {
        if (!isInitialized ||
            !MagnificationNativeMethods.MagGetFullscreenTransform(out float zoomFactor, out _, out _))
        {
            return new(false, 1);
        }

        return new(true, Math.Max(1, zoomFactor));
    }

    public bool ZoomIn() =>
        SendShortcut(VIRTUAL_KEY.VK_ADD);

    public bool ZoomOut() =>
        SendShortcut(VIRTUAL_KEY.VK_SUBTRACT);

    public bool Close() =>
        SendShortcut(VIRTUAL_KEY.VK_ESCAPE);

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        if (isInitialized)
        {
            MagnificationNativeMethods.MagUninitialize();
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
}
