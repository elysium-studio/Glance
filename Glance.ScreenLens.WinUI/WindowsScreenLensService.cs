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
    private const double IdealOcrWordHeight = 40;
    private const int SmVirtualScreenHeight = 79;
    private const int SmVirtualScreenWidth = 78;
    private const int SmVirtualScreenX = 76;
    private const int SmVirtualScreenY = 77;
    private const uint SourceCopy = 0x00CC0020;
    private const int ShowWindowHide = 0;
    private const int ShowWindowShowNoActivate = 4;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly IGlanceIntentService intentService;
    private readonly ITextLocalizer localizer;

    public WindowsScreenLensService(ModuleResourceTextLocalizer<ScreenLensModule> localizer,
        IGlanceIntentService intentService)
    {
        this.localizer = localizer;
        this.intentService = intentService;
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
            await LensSelectionWindow.RunAsync(desktop, localizer, intentService, rectangle => RecognizeAsync(desktop.Crop(rectangle)), CopyAsync);
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

        double initialScale = Math.Min(1, OcrEngine.MaxImageDimension / (double)Math.Max(bitmap.Width, bitmap.Height));
        int initialWidth = Math.Max(1, (int)Math.Floor(bitmap.Width * initialScale));
        int initialHeight = Math.Max(1, (int)Math.Floor(bitmap.Height * initialScale));
        byte[] initialPixels = ScalePixels(bitmap.Pixels, bitmap.Width, bitmap.Height, initialWidth, initialHeight);
        IReadOnlyList<LensRecognizedWord> initialWords = await RecognizePassAsync(engine, bitmap, initialPixels, initialWidth, initialHeight);
        double scale = GetIdealOcrScale(bitmap, initialWords);
        int recognitionWidth = Math.Max(1, (int)Math.Floor(bitmap.Width * scale));
        int recognitionHeight = Math.Max(1, (int)Math.Floor(bitmap.Height * scale));
        byte[] scaledPixels = ScalePixels(bitmap.Pixels, bitmap.Width, bitmap.Height, recognitionWidth, recognitionHeight);
        IReadOnlyList<LensRecognizedWord> originalWords = recognitionWidth == initialWidth && recognitionHeight == initialHeight
            ? initialWords
            : await RecognizePassAsync(engine, bitmap, scaledPixels, recognitionWidth, recognitionHeight);
        byte[] enhancedPixels = EnhanceTextContrast(scaledPixels);
        IReadOnlyList<LensRecognizedWord> enhancedWords = await RecognizePassAsync(engine, bitmap, enhancedPixels, recognitionWidth, recognitionHeight);
        List<LensRecognizedWord> mergedWords = [.. initialWords];
        MergeUniqueWords(mergedWords, originalWords);
        MergeUniqueWords(mergedWords, enhancedWords);
        return BuildRecognitionResult(mergedWords);
    }

    private static double GetIdealOcrScale(LensBitmap bitmap, IReadOnlyList<LensRecognizedWord> words)
    {
        double averageWordHeight = words.Count == 0 ? 10 : words.Average(word => (double)word.Bounds.Height);
        double idealScale = IdealOcrWordHeight / averageWordHeight;
        double maximumScale = OcrEngine.MaxImageDimension / (double)Math.Max(bitmap.Width, bitmap.Height);
        return Math.Min(idealScale, maximumScale);
    }

    private static void MergeUniqueWords(List<LensRecognizedWord> mergedWords, IEnumerable<LensRecognizedWord> candidates)
    {
        foreach (LensRecognizedWord word in candidates)
        {
            if (!mergedWords.Any(existing => RepresentsSameWord(existing.Bounds, word.Bounds)))
            {
                mergedWords.Add(word);
            }
        }
    }

    private static LensRecognitionResult BuildRecognitionResult(IReadOnlyList<LensRecognizedWord> words)
    {
        List<List<LensRecognizedWord>> rows = [];

        foreach (LensRecognizedWord word in words.OrderBy(candidate => candidate.Bounds.Y + (candidate.Bounds.Height / 2)).ThenBy(candidate => candidate.Bounds.X))
        {
            List<LensRecognizedWord>? row = rows
                .Where(candidate => SharesRow(candidate, word))
                .MinBy(candidate => Math.Abs(candidate.Average(item => item.Bounds.Y + (item.Bounds.Height / 2.0)) -
                    (word.Bounds.Y + (word.Bounds.Height / 2.0))));

            if (row is null)
            {
                row = [];
                rows.Add(row);
            }

            row.Add(word);
        }

        rows = [.. rows.OrderBy(row => row.Min(word => word.Bounds.Y)).ThenBy(row => row.Min(word => word.Bounds.X))];
        List<LensRecognizedLine> lines = [];
        List<LensRecognizedWord> orderedWords = [];

        for (int lineIndex = 0; lineIndex < rows.Count; lineIndex++)
        {
            List<LensRecognizedWord> row = [.. rows[lineIndex].OrderBy(word => word.Bounds.X)];
            int left = row.Min(word => word.Bounds.X);
            int top = row.Min(word => word.Bounds.Y);
            int right = row.Max(word => word.Bounds.Right);
            int bottom = row.Max(word => word.Bounds.Bottom);
            string text = string.Join(' ', row.Select(word => word.Text));
            lines.Add(new LensRecognizedLine(text, new LensRectangle(left, top, right - left, bottom - top)));

            for (int wordIndex = 0; wordIndex < row.Count; wordIndex++)
            {
                orderedWords.Add(row[wordIndex] with { LineIndex = lineIndex, WordIndex = wordIndex });
            }
        }

        return new LensRecognitionResult(string.Join(Environment.NewLine, lines.Select(line => line.Text)), lines, orderedWords);
    }

    private static byte[] EnhanceTextContrast(byte[] pixels)
    {
        int pixelCount = pixels.Length / 4;
        int[] luminanceHistogram = new int[256];
        int[] contrastHistogram = new int[256];

        for (int index = 0; index < pixels.Length; index += 4)
        {
            byte blue = pixels[index];
            byte green = pixels[index + 1];
            byte red = pixels[index + 2];
            int luminance = ((54 * red) + (183 * green) + (19 * blue)) >> 8;
            luminanceHistogram[luminance]++;
        }

        bool darkBackground = FindPercentile(luminanceHistogram, pixelCount, 0.5) < 128;

        for (int index = 0; index < pixels.Length; index += 4)
        {
            byte blue = pixels[index];
            byte green = pixels[index + 1];
            byte red = pixels[index + 2];
            int value = darkBackground ? Math.Max(red, Math.Max(green, blue)) : Math.Min(red, Math.Min(green, blue));
            contrastHistogram[value]++;
        }

        int low = FindPercentile(contrastHistogram, pixelCount, 0.005);
        int high = FindPercentile(contrastHistogram, pixelCount, 0.995);

        if (high - low < 32)
        {
            low = 0;
            high = 255;
        }

        byte[] enhanced = new byte[pixels.Length];

        for (int index = 0; index < pixels.Length; index += 4)
        {
            byte blue = pixels[index];
            byte green = pixels[index + 1];
            byte red = pixels[index + 2];
            int value = darkBackground ? Math.Max(red, Math.Max(green, blue)) : Math.Min(red, Math.Min(green, blue));
            byte contrast = (byte)Math.Clamp(((value - low) * 255) / Math.Max(1, high - low), 0, 255);
            enhanced[index] = contrast;
            enhanced[index + 1] = contrast;
            enhanced[index + 2] = contrast;
            enhanced[index + 3] = byte.MaxValue;
        }

        return enhanced;
    }

    private static int FindPercentile(IReadOnlyList<int> histogram, int total, double percentile)
    {
        int target = Math.Max(1, (int)Math.Ceiling(total * percentile));
        int count = 0;

        for (int value = 0; value < histogram.Count; value++)
        {
            count += histogram[value];

            if (count >= target)
            {
                return value;
            }
        }

        return histogram.Count - 1;
    }

    private static async Task<IReadOnlyList<LensRecognizedWord>> RecognizePassAsync(OcrEngine engine,
        LensBitmap source,
        byte[] pixels,
        int width,
        int height)
    {
        using SoftwareBitmap softwareBitmap = new(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Ignore);
        softwareBitmap.CopyFromBuffer(pixels.AsBuffer());
        OcrResult result = await engine.RecognizeAsync(softwareBitmap);
        double scaleX = source.Width / (double)width;
        double scaleY = source.Height / (double)height;
        List<LensRecognizedWord> words = [];

        for (int lineIndex = 0; lineIndex < result.Lines.Count; lineIndex++)
        {
            OcrLine line = result.Lines[lineIndex];

            for (int wordIndex = 0; wordIndex < line.Words.Count; wordIndex++)
            {
                OcrWord word = line.Words[wordIndex];
                Windows.Foundation.Rect bounds = word.BoundingRect;
                int left = Math.Clamp((int)Math.Floor(bounds.X * scaleX), 0, source.Width - 1);
                int top = Math.Clamp((int)Math.Floor(bounds.Y * scaleY), 0, source.Height - 1);
                int right = Math.Clamp((int)Math.Ceiling(bounds.Right * scaleX), left + 1, source.Width);
                int bottom = Math.Clamp((int)Math.Ceiling(bounds.Bottom * scaleY), top + 1, source.Height);
                words.Add(new LensRecognizedWord(word.Text,
                    new LensRectangle(source.OriginX + left, source.OriginY + top, right - left, bottom - top),
                    lineIndex,
                    wordIndex));
            }
        }

        return words;
    }

    private static bool RepresentsSameWord(LensRectangle first, LensRectangle second)
    {
        int left = Math.Max(first.X, second.X);
        int top = Math.Max(first.Y, second.Y);
        int right = Math.Min(first.Right, second.Right);
        int bottom = Math.Min(first.Bottom, second.Bottom);

        if (right <= left || bottom <= top)
        {
            return false;
        }

        double intersection = (right - left) * (bottom - top);
        double smallerArea = Math.Min(first.Width * first.Height, second.Width * second.Height);
        return intersection / Math.Max(1, smallerArea) >= 0.55;
    }

    private static byte[] ScalePixels(byte[] pixels, int width, int height, int scaledWidth, int scaledHeight)
    {
        if (width == scaledWidth && height == scaledHeight)
        {
            return [.. pixels];
        }

        byte[] scaled = new byte[scaledWidth * scaledHeight * 4];

        for (int y = 0; y < scaledHeight; y++)
        {
            int sourceY = Math.Min(height - 1, (int)((long)y * height / scaledHeight));

            for (int x = 0; x < scaledWidth; x++)
            {
                int sourceX = Math.Min(width - 1, (int)((long)x * width / scaledWidth));
                int sourceIndex = ((sourceY * width) + sourceX) * 4;
                int destinationIndex = ((y * scaledWidth) + x) * 4;
                scaled[destinationIndex] = pixels[sourceIndex];
                scaled[destinationIndex + 1] = pixels[sourceIndex + 1];
                scaled[destinationIndex + 2] = pixels[sourceIndex + 2];
                scaled[destinationIndex + 3] = byte.MaxValue;
            }
        }

        return scaled;
    }

    private static bool SharesRow(IReadOnlyList<LensRecognizedWord> row, LensRecognizedWord candidate)
    {
        int rowTop = row.Min(word => word.Bounds.Y);
        int rowBottom = row.Max(word => word.Bounds.Bottom);
        int overlap = Math.Min(rowBottom, candidate.Bounds.Bottom) - Math.Max(rowTop, candidate.Bounds.Y);
        int minimumHeight = Math.Min(rowBottom - rowTop, candidate.Bounds.Height);
        double rowCenter = (rowTop + rowBottom) / 2.0;
        double candidateCenter = candidate.Bounds.Y + (candidate.Bounds.Height / 2.0);
        return overlap >= minimumHeight * 0.35 || Math.Abs(rowCenter - candidateCenter) <= Math.Max(3, minimumHeight * 0.45);
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
