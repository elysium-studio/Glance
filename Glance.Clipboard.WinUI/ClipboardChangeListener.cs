using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Glance.Clipboard.WinUI;

internal sealed unsafe class ClipboardChangeListener : IDisposable
{
    private const uint ClipboardUpdateMessage = 0x031D;

    private readonly HINSTANCE moduleHandle;
    private readonly string windowClassName = $"Glance.Clipboard.{Guid.NewGuid():N}";
    private readonly WNDPROC windowProcedure;
    private ushort classAtom;
    private bool disposed;
    private HWND windowHandle;

    public ClipboardChangeListener()
    {
        windowProcedure = HandleWindowMessage;
        moduleHandle = PInvoke.GetModuleHandle((PCWSTR)null);

        fixed (char* className = windowClassName)
        {
            WNDCLASSW windowClass = new()
            {
                hInstance = moduleHandle,
                lpfnWndProc = windowProcedure,
                lpszClassName = className
            };

            classAtom = PInvoke.RegisterClass(in windowClass);
            if (classAtom == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            windowHandle = PInvoke.CreateWindowEx(WINDOW_EX_STYLE.WS_EX_NOACTIVATE, className, default, WINDOW_STYLE.WS_OVERLAPPED, 0, 0, 0, 0, HWND.HWND_MESSAGE, default, moduleHandle, null);
        }

        if (windowHandle.IsNull)
        {
            Dispose();
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if (!PInvoke.AddClipboardFormatListener(windowHandle))
        {
            Dispose();
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public event EventHandler? ClipboardChanged;

    public HWND WindowHandle => windowHandle;

    public nint Handle => (nint)windowHandle.Value;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (!windowHandle.IsNull)
        {
            PInvoke.RemoveClipboardFormatListener(windowHandle);
            PInvoke.DestroyWindow(windowHandle);
            windowHandle = default;
        }

        if (classAtom != 0)
        {
            fixed (char* className = windowClassName)
            {
                PInvoke.UnregisterClass(className, moduleHandle);
            }

            classAtom = 0;
        }
    }

    private LRESULT HandleWindowMessage(
        HWND window,
        uint message,
        WPARAM wParam,
        LPARAM lParam)
    {
        if (message == ClipboardUpdateMessage)
        {
            using IDisposable operation = ClipboardDiagnostics.Begin("Notification");

            try
            {
                ClipboardChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception exception)
            {
                ClipboardDiagnostics.WriteException("NotificationFailed", exception);
            }

            return default;
        }

        return PInvoke.DefWindowProc(window, message, wParam, lParam);
    }
}
