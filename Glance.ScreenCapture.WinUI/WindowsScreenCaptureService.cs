using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Glance.ScreenCapture.WinUI;

public sealed partial class WindowsScreenCaptureService : IScreenCaptureService
{
    private const uint CaptureBlt = 0x40000000;
    private const int DwmExtendedFrameBounds = 9;
    private const int SmVirtualScreenHeight = 79;
    private const int SmVirtualScreenWidth = 78;
    private const int SmVirtualScreenX = 76;
    private const int SmVirtualScreenY = 77;
    private const uint SourceCopy = 0x00CC0020;
    private const int SwHide = 0;
    private const int SwShowNoActivate = 8;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ITextLocalizer localizer;
    private readonly string captureFolderPath;

    public WindowsScreenCaptureService(ModuleResourceTextLocalizer<ScreenCaptureModule> localizer)
    {
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        captureFolderPath = ResolveCaptureFolderPath();
    }

    public async Task<ScreenCaptureItem?> CaptureAsync(ScreenCaptureMode mode)
    {
        if (!dispatcherQueue.HasThreadAccess)
        {
            throw new InvalidOperationException("Screen capture must begin on the UI thread.");
        }

        IReadOnlyList<NativeRectangle> candidates = mode switch
        {
            ScreenCaptureMode.Window => EnumerateWindowRectangles(),
            ScreenCaptureMode.Display => EnumerateDisplayRectangles(),
            _ => []
        };
        IReadOnlyList<nint> applicationWindows = GetVisibleApplicationWindows();

        try
        {
            SetWindowsVisible(applicationWindows, false);
            await Task.Delay(140);

            DesktopCaptureBitmap desktop = CaptureVirtualDesktop();
            NativeRectangle? selection = mode switch
            {
                ScreenCaptureMode.Region => await CaptureSelectionWindow.SelectAsync(desktop, mode, [], localizer, dispatcherQueue),
                ScreenCaptureMode.Window => await CaptureSelectionWindow.SelectAsync(desktop, mode, candidates, localizer, dispatcherQueue),
                ScreenCaptureMode.Display => await CaptureSelectionWindow.SelectAsync(desktop, mode, candidates, localizer, dispatcherQueue),
                ScreenCaptureMode.AllDisplays => desktop.Bounds,
                _ => null
            };

            if (selection is null)
            {
                return null;
            }

            DesktopCaptureBitmap result = selection.Value == desktop.Bounds
                ? desktop
                : desktop.Crop(selection.Value);
            return await SaveAsync(result, mode);
        }
        finally
        {
            SetWindowsVisible(applicationWindows, true);
        }
    }

    public IReadOnlyList<ScreenCaptureItem> GetRecentCaptures(int maximumCount)
    {
        try
        {
            if (!Directory.Exists(captureFolderPath))
            {
                return [];
            }

            return Directory.EnumerateFiles(captureFolderPath, "Glance *.png")
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.CreationTimeUtc)
                .Take(maximumCount)
                .Select(CreateRecentItem)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    public bool TryOpen(ScreenCaptureItem capture) => StartProcess(capture.FilePath);

    public bool TryReveal(ScreenCaptureItem capture)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{capture.FilePath}\"",
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TryCopyAsync(ScreenCaptureItem capture)
    {
        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(capture.FilePath);

            for (int attempt = 0; attempt < 6; attempt++)
            {
                try
                {
                    DataPackage package = new();
                    package.SetBitmap(RandomAccessStreamReference.CreateFromFile(file));
                    Clipboard.SetContent(package);
                    Clipboard.Flush();
                    return true;
                }
                catch (COMException) when (attempt < 5)
                {
                    await Task.Delay(40 * (attempt + 1));
                }
            }
        }
        catch
        {
        }

        return false;
    }

    public bool TryDelete(ScreenCaptureItem capture)
    {
        try
        {
            File.Delete(capture.FilePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static DesktopCaptureBitmap CaptureVirtualDesktop()
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

            BitmapInfo bitmapInfo = new()
            {
                Header = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    Width = width,
                    Height = -height,
                    Planes = 1,
                    BitCount = 32,
                    Compression = 0
                }
            };
            byte[] pixels = new byte[width * height * 4];

            if (NativeMethods.GetDIBits(memoryDeviceContext, bitmap, 0, (uint)height, pixels, ref bitmapInfo, 0) == 0)
            {
                throw new InvalidOperationException("Unable to read the desktop pixels.");
            }

            return new DesktopCaptureBitmap(x, y, width, height, pixels);
        }
        finally
        {
            _ = NativeMethods.SelectObject(memoryDeviceContext, previousBitmap);
            _ = NativeMethods.DeleteObject(bitmap);
            _ = NativeMethods.DeleteDC(memoryDeviceContext);
            _ = NativeMethods.ReleaseDC(nint.Zero, screenDeviceContext);
        }
    }

    private async Task<ScreenCaptureItem> SaveAsync(DesktopCaptureBitmap bitmap, ScreenCaptureMode mode)
    {
        Directory.CreateDirectory(captureFolderPath);
        StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(captureFolderPath);
        string fileName = $"Glance {DateTime.Now:yyyy-MM-dd HH-mm-ss}.png";
        StorageFile file = await folder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

        using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)bitmap.Width, (uint)bitmap.Height, 96, 96, bitmap.Pixels);
        await encoder.FlushAsync();

        return new ScreenCaptureItem(file.Path, file.Name, DateTimeOffset.Now, bitmap.Width, bitmap.Height, mode);
    }

    private static ScreenCaptureItem CreateRecentItem(FileInfo file)
    {
        (int width, int height) = ReadPngSize(file.FullName);
        return new ScreenCaptureItem(file.FullName, file.Name, file.CreationTime, width, height, ScreenCaptureMode.Region);
    }

    private static (int Width, int Height) ReadPngSize(string path)
    {
        try
        {
            Span<byte> header = stackalloc byte[24];
            using FileStream stream = File.OpenRead(path);

            if (stream.Read(header) != header.Length)
            {
                return (0, 0);
            }

            int width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            int height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            return (width, height);
        }
        catch
        {
            return (0, 0);
        }
    }

    private static string ResolveCaptureFolderPath()
    {
        try
        {
            string path = KnownFolders.AppCaptures.Path;

            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }
        }
        catch
        {
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Captures");
    }

    private static bool StartProcess(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<nint> GetVisibleApplicationWindows()
    {
        uint processId = (uint)Environment.ProcessId;
        List<nint> windows = [];
        NativeMethods.EnumWindows((window, parameter) =>
        {
            NativeMethods.GetWindowThreadProcessId(window, out uint windowProcessId);

            if (windowProcessId == processId && NativeMethods.IsWindowVisible(window))
            {
                windows.Add(window);
            }

            return true;
        }, nint.Zero);
        return windows;
    }

    private static void SetWindowsVisible(IEnumerable<nint> windows, bool visible)
    {
        foreach (nint window in windows)
        {
            _ = NativeMethods.ShowWindow(window, visible ? SwShowNoActivate : SwHide);
        }
    }

    private static IReadOnlyList<NativeRectangle> EnumerateWindowRectangles()
    {
        uint processId = (uint)Environment.ProcessId;
        List<NativeRectangle> rectangles = [];
        NativeMethods.EnumWindows((window, parameter) =>
        {
            NativeMethods.GetWindowThreadProcessId(window, out uint windowProcessId);

            if (windowProcessId == processId || !NativeMethods.IsWindowVisible(window) || NativeMethods.IsIconic(window))
            {
                return true;
            }

            NativeRect rectangle;

            if (NativeMethods.DwmGetWindowAttribute(window, DwmExtendedFrameBounds, out rectangle, (uint)Marshal.SizeOf<NativeRect>()) != 0 && !NativeMethods.GetWindowRect(window, out rectangle))
            {
                return true;
            }

            int width = rectangle.Right - rectangle.Left;
            int height = rectangle.Bottom - rectangle.Top;

            if (width >= 80 && height >= 60)
            {
                rectangles.Add(new NativeRectangle(rectangle.Left, rectangle.Top, width, height));
            }

            return true;
        }, nint.Zero);
        return rectangles;
    }

    private static IReadOnlyList<NativeRectangle> EnumerateDisplayRectangles()
    {
        List<NativeRectangle> rectangles = [];
        NativeMethods.EnumDisplayMonitors(nint.Zero, nint.Zero, (monitor, _, _, _) =>
        {
            MonitorInfo info = new() { Size = (uint)Marshal.SizeOf<MonitorInfo>() };

            if (NativeMethods.GetMonitorInfo(monitor, ref info))
            {
                rectangles.Add(new NativeRectangle(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right - info.Monitor.Left, info.Monitor.Bottom - info.Monitor.Top));
            }

            return true;
        }, nint.Zero);
        return rectangles;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool EnumWindowsCallback(nint window, nint parameter);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool EnumDisplayMonitorsCallback(nint monitor, nint deviceContext, nint rectangle, nint parameter);

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
        public static partial bool EnumDisplayMonitors(nint deviceContext, nint clip, EnumDisplayMonitorsCallback callback, nint parameter);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool IsWindowVisible(nint window);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool IsIconic(nint window);

        [LibraryImport("user32.dll")]
        public static partial uint GetWindowThreadProcessId(nint window, out uint processId);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetWindowRect(nint window, out NativeRect rectangle);

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmGetWindowAttribute(nint window, int attribute, out NativeRect value, uint size);

        [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ShowWindow(nint window, int command);
    }
}
