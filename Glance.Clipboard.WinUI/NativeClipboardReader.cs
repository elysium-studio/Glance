using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Glance.Clipboard.WinUI;

internal static class NativeClipboardReader
{
    private const uint ClipboardFormatUnicodeText = 13;
    private const uint ClipboardFormatFileDrop = 15;
    private const int MaximumAttempts = 8;
    private const nuint MaximumPayloadBytes = 32 * 1024 * 1024;

    public static async Task<NativeClipboardCapture> CaptureAsync()
    {
        for (int attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            if (PInvoke.OpenClipboard(HWND.Null))
            {
                try
                {
                    ClipboardSnapshot snapshot = CaptureOpenClipboard();
                    return new NativeClipboardCapture(
                        true,
                        snapshot.HasContent ? snapshot : null);
                }
                finally
                {
                    _ = PInvoke.CloseClipboard();
                }
            }

            await Task.Delay(25);
        }

        return new NativeClipboardCapture(false, null);
    }

    private static ClipboardSnapshot CaptureOpenClipboard()
    {
        ClipboardSnapshot snapshot = new()
        {
            Text = ReadUnicodeText(ClipboardFormatUnicodeText),
            FilePaths = ReadFileDropPaths()
        };

        uint htmlFormat = PInvoke.RegisterClipboardFormat("HTML Format");
        uint rtfFormat = PInvoke.RegisterClipboardFormat("Rich Text Format");
        uint pngFormat = PInvoke.RegisterClipboardFormat("PNG");

        snapshot.Html = ReadEncodedText(htmlFormat, Encoding.UTF8);
        snapshot.Rtf = ReadEncodedText(rtfFormat, Encoding.UTF8);
        snapshot.Bitmap = ReadBytes(pngFormat);

        if (snapshot.Text is not null &&
            Uri.TryCreate(snapshot.Text, UriKind.Absolute, out Uri? link) &&
            (link.Scheme == Uri.UriSchemeHttp || link.Scheme == Uri.UriSchemeHttps))
        {
            snapshot.WebLink = link.ToString();
        }

        return snapshot;
    }

    private static string? ReadUnicodeText(uint format)
    {
        byte[]? bytes = ReadBytes(format);
        if (bytes is null || bytes.Length < sizeof(char))
        {
            return null;
        }

        string text = Encoding.Unicode.GetString(bytes);
        int terminator = text.IndexOf('\0');
        if (terminator >= 0)
        {
            text = text[..terminator];
        }

        return text.Length == 0 ? null : text;
    }

    private static string? ReadEncodedText(uint format, Encoding encoding)
    {
        byte[]? bytes = ReadBytes(format);
        if (bytes is null)
        {
            return null;
        }

        int terminator = Array.IndexOf(bytes, (byte)0);
        int length = terminator >= 0 ? terminator : bytes.Length;
        return length == 0 ? null : encoding.GetString(bytes, 0, length);
    }

    private static unsafe byte[]? ReadBytes(uint format)
    {
        if (format == 0 || !PInvoke.IsClipboardFormatAvailable(format))
        {
            return null;
        }

        HANDLE handle = PInvoke.GetClipboardData(format);
        if (handle.IsNull)
        {
            return null;
        }

        HGLOBAL global = new(handle.Value);
        nuint size = PInvoke.GlobalSize(global);
        if (size == 0 || size > MaximumPayloadBytes)
        {
            return null;
        }

        void* source = PInvoke.GlobalLock(global);
        if (source is null)
        {
            return null;
        }

        try
        {
            byte[] bytes = new byte[(int)size];
            Marshal.Copy((nint)source, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            _ = PInvoke.GlobalUnlock(global);
        }
    }

    private static string[] ReadFileDropPaths()
    {
        byte[]? fileDrop = ReadBytes(ClipboardFormatFileDrop);
        if (fileDrop is null || fileDrop.Length < 20)
        {
            return [];
        }

        int pathsOffset = BitConverter.ToInt32(fileDrop, 0);
        bool usesUnicode = BitConverter.ToInt32(fileDrop, 16) != 0;
        if (pathsOffset < 20 || pathsOffset >= fileDrop.Length)
        {
            return [];
        }

        string paths = usesUnicode
            ? Encoding.Unicode.GetString(fileDrop, pathsOffset, fileDrop.Length - pathsOffset)
            : Encoding.Default.GetString(fileDrop, pathsOffset, fileDrop.Length - pathsOffset);

        return paths.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}

internal readonly record struct NativeClipboardCapture(
    bool WasRead,
    ClipboardSnapshot? Snapshot);
