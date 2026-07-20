using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;

namespace Glance.Clipboard.WinUI;

internal static class NativeClipboardWriter
{
    private const uint ClipboardFormatUnicodeText = 13;
    private const uint ClipboardFormatFileDrop = 15;
    private const int MaximumAttempts = 8;

    public static async Task<bool> WriteAsync(
        ClipboardSnapshot snapshot,
        HWND ownerWindow)
    {
        if (ownerWindow.IsNull)
        {
            return false;
        }

        for (int attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            if (PInvoke.OpenClipboard(ownerWindow))
            {
                try
                {
                    return WriteOpenClipboard(snapshot);
                }
                finally
                {
                    _ = PInvoke.CloseClipboard();
                }
            }

            await Task.Delay(25);
        }

        return false;
    }

    public static async Task<bool> ClearAsync(HWND ownerWindow)
    {
        if (ownerWindow.IsNull)
        {
            return false;
        }

        for (int attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            if (PInvoke.OpenClipboard(ownerWindow))
            {
                try
                {
                    return PInvoke.EmptyClipboard();
                }
                finally
                {
                    _ = PInvoke.CloseClipboard();
                }
            }

            await Task.Delay(25);
        }

        return false;
    }

    private static bool WriteOpenClipboard(ClipboardSnapshot snapshot)
    {
        if (!PInvoke.EmptyClipboard())
        {
            return false;
        }

        bool wroteContent = false;
        string? text = snapshot.Text ?? snapshot.WebLink ?? snapshot.ApplicationLink;

        if (text is not null)
        {
            wroteContent |= SetClipboardBytes(
                ClipboardFormatUnicodeText,
                Encoding.Unicode.GetBytes($"{text}\0"));
        }

        if (snapshot.Html is not null)
        {
            wroteContent |= SetClipboardBytes(
                PInvoke.RegisterClipboardFormat("HTML Format"),
                EncodeNullTerminated(snapshot.Html, Encoding.UTF8));
        }

        if (snapshot.Rtf is not null)
        {
            wroteContent |= SetClipboardBytes(
                PInvoke.RegisterClipboardFormat("Rich Text Format"),
                EncodeNullTerminated(snapshot.Rtf, Encoding.UTF8));
        }

        if (snapshot.Bitmap is not null)
        {
            wroteContent |= SetClipboardBytes(
                PInvoke.RegisterClipboardFormat("PNG"),
                snapshot.Bitmap);
        }

        if (snapshot.FilePaths is { Count: > 0 })
        {
            wroteContent |= SetClipboardBytes(
                ClipboardFormatFileDrop,
                CreateFileDropPayload(snapshot.FilePaths));
        }

        return wroteContent;
    }

    private static byte[] EncodeNullTerminated(string value, Encoding encoding)
    {
        byte[] valueBytes = encoding.GetBytes(value);
        byte[] bytes = new byte[valueBytes.Length + 1];
        Buffer.BlockCopy(valueBytes, 0, bytes, 0, valueBytes.Length);
        return bytes;
    }

    private static byte[] CreateFileDropPayload(IReadOnlyList<string> paths)
    {
        const int dropFilesHeaderSize = 20;
        string pathList = $"{string.Join('\0', paths)}\0\0";
        byte[] pathBytes = Encoding.Unicode.GetBytes(pathList);
        byte[] payload = new byte[dropFilesHeaderSize + pathBytes.Length];

        BitConverter.GetBytes(dropFilesHeaderSize).CopyTo(payload, 0);
        BitConverter.GetBytes(1).CopyTo(payload, 16);
        Buffer.BlockCopy(pathBytes, 0, payload, dropFilesHeaderSize, pathBytes.Length);
        return payload;
    }

    private static unsafe bool SetClipboardBytes(uint format, byte[] bytes)
    {
        if (format == 0 || bytes.Length == 0)
        {
            return false;
        }

        HGLOBAL memory = PInvoke.GlobalAlloc(
            GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE | GLOBAL_ALLOC_FLAGS.GMEM_ZEROINIT,
            (nuint)bytes.Length);

        if (memory.IsNull)
        {
            return false;
        }

        bool ownershipTransferred = false;

        try
        {
            void* destination = PInvoke.GlobalLock(memory);
            if (destination is null)
            {
                return false;
            }

            try
            {
                Marshal.Copy(bytes, 0, (nint)destination, bytes.Length);
            }
            finally
            {
                _ = PInvoke.GlobalUnlock(memory);
            }

            HANDLE result = PInvoke.SetClipboardData(format, new HANDLE(memory.Value));
            ownershipTransferred = !result.IsNull;
            return ownershipTransferred;
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
