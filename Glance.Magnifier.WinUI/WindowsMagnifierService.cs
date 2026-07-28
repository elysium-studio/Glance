using System;
using System.Collections.Generic;
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
        return SendShortcut(VIRTUAL_KEY.VK_ESCAPE);
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
        HashSet<uint> processIds = [];

        foreach (Process process in Process.GetProcessesByName("Magnify"))
        {
            using (process)
            {
                try
                {
                    processIds.Add((uint)process.Id);
                }
                catch (InvalidOperationException)
                { }
            }
        }

        if (processIds.Count == 0)
        {
            return;
        }

        MagnificationNativeMethods.EnumWindows((window, _) =>
        {
            MagnificationNativeMethods.GetWindowThreadProcessId(window, out uint processId);

            if (processIds.Contains(processId) &&
                IsNativeMagnifierSurface(window))
            {
                MagnificationNativeMethods.ShowWindow(window, 0);
            }

            return true;
        }, nint.Zero);
    }

    private static bool IsNativeMagnifierSurface(nint window)
    {
        string className = GetWindowClassName(window);

        if (className is "MagUIClass" or "ScreenMagnifierUIWnd")
        {
            return true;
        }

        return GetWindowTitle(window).Contains("Magnifier Touch", StringComparison.OrdinalIgnoreCase);
    }

    private static unsafe string GetWindowClassName(nint window)
    {
        char* buffer = stackalloc char[256];
        int length = MagnificationNativeMethods.GetClassName(window, buffer, 256);
        return length > 0 ? new(buffer, 0, length) : string.Empty;
    }

    private static unsafe string GetWindowTitle(nint window)
    {
        char* buffer = stackalloc char[256];
        int length = MagnificationNativeMethods.GetWindowText(window, buffer, 256);
        return length > 0 ? new(buffer, 0, length) : string.Empty;
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
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate bool EnumWindowsCallback(nint window,
        nint parameter);

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

    [LibraryImport("user32.dll", EntryPoint = "EnumWindows")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumWindows(EnumWindowsCallback callback,
        nint parameter);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")]
    public static partial uint GetWindowThreadProcessId(nint window,
        out uint processId);

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW")]
    public static unsafe partial int GetClassName(nint window,
        char* className,
        int maximumCount);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW")]
    public static unsafe partial int GetWindowText(nint window,
        char* text,
        int maximumCount);

    [LibraryImport("user32.dll", EntryPoint = "ShowWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint window,
        int command);
}
