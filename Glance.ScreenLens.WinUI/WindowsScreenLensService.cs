using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Glance.ScreenLens.WinUI;

public sealed partial class WindowsScreenLensService :
    IScreenLensService
{
    private const uint CaptureBlt = 0x40000000;
    private const int SmVirtualScreenHeight = 79;
    private const int SmVirtualScreenWidth = 78;
    private const int SmVirtualScreenX = 76;
    private const int SmVirtualScreenY = 77;
    private const uint SourceCopy = 0x00CC0020;
    private const int ShowWindowHide = 0;
    private const int ShowWindowShowNoActivate = 4;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ITextLocalizer localizer;

    public WindowsScreenLensService(ModuleResourceTextLocalizer<ScreenLensModule> localizer)
    {
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    public async Task ExtractAsync()
    {
        if (!dispatcherQueue.HasThreadAccess)
        {
            throw new InvalidOperationException("Screen Lens must begin on the UI thread.");
        }

        IReadOnlyList<ApplicationWindowState> applicationWindows = GetVisibleApplicationWindows();

        try
        {
            HideApplicationWindows(applicationWindows);
            _ = NativeMethods.DwmFlush();
            LensBitmap desktop = CaptureVirtualDesktop();
            await LensSelectionWindow.RunAsync(desktop, localizer, rectangle => RecognizeAsync(desktop.Crop(rectangle)), CopyAsync);
        }
        finally
        {
            RestoreWindows(applicationWindows);
            _ = NativeMethods.DwmFlush();
        }
    }

    private static async Task<bool> CopyAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        for (int attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                DataPackage package = new();
                package.SetText(text);
                Clipboard.SetContent(package);
                Clipboard.Flush();
                return true;
            }
            catch (COMException)
            {
                if (attempt == 5)
                {
                    return false;
                }

                await Task.Delay(40 * (attempt + 1));
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private static LensBitmap CaptureVirtualDesktop()
    {
        int x = NativeMethods.GetSystemMetrics(SmVirtualScreenX);
        int y = NativeMethods.GetSystemMetrics(SmVirtualScreenY);
        int width = NativeMethods.GetSystemMetrics(SmVirtualScreenWidth);
        int height = NativeMethods.GetSystemMetrics(SmVirtualScreenHeight);
        nint screenDeviceContext = NativeMethods.GetDC(nint.Zero);

        if (screenDeviceContext == nint.Zero)
        {
            throw new InvalidOperationException("Unable to access the desktop surface.");
        }

        nint memoryDeviceContext = NativeMethods.CreateCompatibleDC(screenDeviceContext);
        nint bitmap = NativeMethods.CreateCompatibleBitmap(screenDeviceContext, width, height);
        nint previousBitmap = NativeMethods.SelectObject(memoryDeviceContext, bitmap);

        try
        {
            if (!NativeMethods.BitBlt(memoryDeviceContext, 0, 0, width, height, screenDeviceContext, x, y, SourceCopy | CaptureBlt))
            {
                throw new InvalidOperationException("Unable to copy the desktop surface.");
            }

            return new LensBitmap(x, y, width, height, ReadBitmapPixels(memoryDeviceContext, bitmap, width, height));
        }
        finally
        {
            _ = NativeMethods.SelectObject(memoryDeviceContext, previousBitmap);
            _ = NativeMethods.DeleteObject(bitmap);
            _ = NativeMethods.DeleteDC(memoryDeviceContext);
            _ = NativeMethods.ReleaseDC(nint.Zero, screenDeviceContext);
        }
    }

    private static IReadOnlyList<ApplicationWindowState> GetVisibleApplicationWindows()
    {
        uint processId = (uint)Environment.ProcessId;
        List<ApplicationWindowState> windows = [];
        NativeMethods.EnumWindows((window, parameter) =>
        {
            NativeMethods.GetWindowThreadProcessId(window, out uint windowProcessId);

            if (windowProcessId == processId && NativeMethods.IsWindowVisible(window))
            {
                windows.Add(new ApplicationWindowState(window));
            }

            return true;
        }, nint.Zero);
        return windows;
    }

    private static void HideApplicationWindows(IEnumerable<ApplicationWindowState> windows)
    {
        foreach (ApplicationWindowState window in windows)
        {
            _ = NativeMethods.ShowWindow(window.Handle, ShowWindowHide);
        }
    }

    private static byte[] ReadBitmapPixels(nint deviceContext, nint bitmap, int width, int height)
    {
        BitmapInfo bitmapInfo = new()
        {
            Header = new BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32
            }
        };
        byte[] pixels = new byte[width * height * 4];

        if (NativeMethods.GetDIBits(deviceContext, bitmap, 0, (uint)height, pixels, ref bitmapInfo, 0) == 0)
        {
            throw new InvalidOperationException("Unable to read the captured pixels.");
        }

        for (int index = 3; index < pixels.Length; index += 4)
        {
            pixels[index] = byte.MaxValue;
        }

        return pixels;
    }

    private static async Task<LensRecognitionResult> RecognizeAsync(LensBitmap bitmap)
    {
        OcrEngine? engine = OcrEngine.TryCreateFromUserProfileLanguages();

        if (engine is null)
        {
            return LensRecognitionResult.Empty;
        }

        using SoftwareBitmap softwareBitmap = new(BitmapPixelFormat.Bgra8, bitmap.Width, bitmap.Height, BitmapAlphaMode.Ignore);
        softwareBitmap.CopyFromBuffer(bitmap.Pixels.AsBuffer());
        OcrResult result = await engine.RecognizeAsync(softwareBitmap);
        string text = string.Join(Environment.NewLine, result.Lines.Select(line => line.Text)).Trim();
        List<LensRecognizedWord> words = [];

        for (int lineIndex = 0; lineIndex < result.Lines.Count; lineIndex++)
        {
            OcrLine line = result.Lines[lineIndex];

            for (int wordIndex = 0; wordIndex < line.Words.Count; wordIndex++)
            {
                OcrWord word = line.Words[wordIndex];
                Windows.Foundation.Rect bounds = word.BoundingRect;
                int left = Math.Clamp((int)Math.Floor(bounds.X), 0, bitmap.Width - 1);
                int top = Math.Clamp((int)Math.Floor(bounds.Y), 0, bitmap.Height - 1);
                int right = Math.Clamp((int)Math.Ceiling(bounds.Right), left + 1, bitmap.Width);
                int bottom = Math.Clamp((int)Math.Ceiling(bounds.Bottom), top + 1, bitmap.Height);
                words.Add(new LensRecognizedWord(word.Text,
                    new LensRectangle(left, top, right - left, bottom - top),
                    lineIndex,
                    wordIndex));
            }
        }

        return new LensRecognitionResult(text, words);
    }

    private static void RestoreWindows(IEnumerable<ApplicationWindowState> windows)
    {
        foreach (ApplicationWindowState window in windows)
        {
            _ = NativeMethods.ShowWindow(window.Handle, ShowWindowShowNoActivate);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPixelsPerMeter;
        public int YPixelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }

    private readonly record struct ApplicationWindowState(nint Handle);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool EnumWindowsCallback(nint window, nint parameter);

    private static partial class NativeMethods
    {
        [LibraryImport("user32.dll")]
        public static partial int GetSystemMetrics(int index);

        [LibraryImport("user32.dll")]
        public static partial nint GetDC(nint window);

        [LibraryImport("user32.dll")]
        public static partial int ReleaseDC(nint window, nint deviceContext);

        [LibraryImport("gdi32.dll")]
        public static partial nint CreateCompatibleDC(nint deviceContext);

        [LibraryImport("gdi32.dll")]
        public static partial nint CreateCompatibleBitmap(nint deviceContext, int width, int height);

        [LibraryImport("gdi32.dll")]
        public static partial nint SelectObject(nint deviceContext, nint value);

        [LibraryImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool BitBlt(nint destination, int destinationX, int destinationY, int width, int height, nint source, int sourceX, int sourceY, uint operation);

        [LibraryImport("gdi32.dll")]
        public static partial int GetDIBits(nint deviceContext, nint bitmap, uint start, uint lines, byte[] pixels, ref BitmapInfo bitmapInfo, uint usage);

        [LibraryImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DeleteObject(nint value);

        [LibraryImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DeleteDC(nint deviceContext);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool EnumWindows(EnumWindowsCallback callback, nint parameter);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool IsWindowVisible(nint window);

        [LibraryImport("user32.dll")]
        public static partial uint GetWindowThreadProcessId(nint window, out uint processId);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ShowWindow(nint window, int command);

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmFlush();
    }
}
