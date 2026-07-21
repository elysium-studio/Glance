using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Glance.ColorPicker.WinUI;

public sealed class WindowsTextCopyService :
    ITextCopyService,
    IDisposable
{
    private const uint ClipboardFormatUnicodeText = 13;
    private const int MaximumAttempts = 8;

    private HWND ownerWindow;

    public unsafe WindowsTextCopyService()
    {
        fixed (char* className = "STATIC")
        {
            ownerWindow = PInvoke.CreateWindowEx(
                WINDOW_EX_STYLE.WS_EX_NOACTIVATE,
                className,
                default,
                WINDOW_STYLE.WS_OVERLAPPED,
                0,
                0,
                0,
                0,
                HWND.HWND_MESSAGE,
                default,
                default,
                null);
        }

        if (ownerWindow.IsNull)
        {
            Debug.WriteLine(
                $"ColorPicker: failed to create clipboard owner window; " +
                $"Error={Marshal.GetLastWin32Error()}");
        }
    }

    public async Task CopyAsync(string text)
    {
        try
        {
            if (ownerWindow.IsNull)
            {
                return;
            }

            for (int attempt = 0; attempt < MaximumAttempts; attempt++)
            {
                if (PInvoke.OpenClipboard(ownerWindow))
                {
                    try
                    {
                        WriteText(text);
                        return;
                    }
                    finally
                    {
                        _ = PInvoke.CloseClipboard();
                    }
                }

                await Task.Delay(25).ConfigureAwait(false);
            }

            Debug.WriteLine(
                $"ColorPicker: failed to open the clipboard after {MaximumAttempts} attempts; " +
                $"Error={Marshal.GetLastWin32Error()}");
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"ColorPicker: failed to copy text: {exception}");
        }
    }

    public void Dispose()
    {
        if (!ownerWindow.IsNull)
        {
            _ = PInvoke.DestroyWindow(ownerWindow);
            ownerWindow = default;
        }
    }

    private static unsafe void WriteText(string text)
    {
        if (!PInvoke.EmptyClipboard())
        {
            throw new COMException("EmptyClipboard failed.", Marshal.GetHRForLastWin32Error());
        }

        byte[] bytes = Encoding.Unicode.GetBytes($"{text}\0");
        HGLOBAL memory = PInvoke.GlobalAlloc(
            GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE | GLOBAL_ALLOC_FLAGS.GMEM_ZEROINIT,
            (nuint)bytes.Length);

        if (memory.IsNull)
        {
            throw new COMException("GlobalAlloc failed.", Marshal.GetHRForLastWin32Error());
        }

        bool ownershipTransferred = false;

        try
        {
            void* destination = PInvoke.GlobalLock(memory);
            if (destination is null)
            {
                throw new COMException("GlobalLock failed.", Marshal.GetHRForLastWin32Error());
            }

            try
            {
                Marshal.Copy(bytes, 0, (nint)destination, bytes.Length);
            }
            finally
            {
                _ = PInvoke.GlobalUnlock(memory);
            }

            HANDLE result = PInvoke.SetClipboardData(
                ClipboardFormatUnicodeText,
                new HANDLE(memory.Value));
            ownershipTransferred = !result.IsNull;

            if (!ownershipTransferred)
            {
                throw new COMException("SetClipboardData failed.", Marshal.GetHRForLastWin32Error());
            }
        }
        finally
        {
            if (!ownershipTransferred)
            {
                _ = PInvoke.GlobalFree(memory);
            }
        }
    }
}
