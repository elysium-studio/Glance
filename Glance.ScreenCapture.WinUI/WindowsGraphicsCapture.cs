using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using WinRT;

namespace Glance.ScreenCapture.WinUI;

internal static partial class WindowsGraphicsCapture
{
    private const uint D3D11CreateDeviceBgraSupport = 0x20;
    private const uint D3D11SdkVersion = 7;
    private const int D3DDriverTypeHardware = 1;
    private static readonly Guid GraphicsCaptureItemGuid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid IdxgiDeviceGuid = new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");

    public static async Task<DesktopCaptureBitmap> CaptureWindowAsync(nint window, NativeRectangle bounds)
    {
        if (!GraphicsCaptureSession.IsSupported())
        {
            throw new NotSupportedException("Windows Graphics Capture is not supported on this device.");
        }

        GraphicsCaptureItem item = CreateItemForWindow(window);
        using IDirect3DDevice device = CreateDevice();
        using Direct3D11CaptureFramePool framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, item.Size);
        using GraphicsCaptureSession session = framePool.CreateCaptureSession(item);
        TaskCompletionSource<DesktopCaptureBitmap> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int frameClaimed = 0;

        async void HandleFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            if (Interlocked.Exchange(ref frameClaimed, 1) != 0)
            {
                return;
            }

            try
            {
                using Direct3D11CaptureFrame frame = sender.TryGetNextFrame();
                using SoftwareBitmap softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface, BitmapAlphaMode.Ignore);
                int width = softwareBitmap.PixelWidth;
                int height = softwareBitmap.PixelHeight;
                byte[] pixels = new byte[width * height * 4];
                softwareBitmap.CopyToBuffer(pixels.AsBuffer());
                DesktopCaptureBitmap bitmap = new(bounds.X, bounds.Y, width, height, pixels);
                int contentWidth = Math.Min(frame.ContentSize.Width, width);
                int contentHeight = Math.Min(frame.ContentSize.Height, height);
                completion.TrySetResult(contentWidth == width && contentHeight == height ? bitmap : bitmap.Crop(new NativeRectangle(bounds.X, bounds.Y, contentWidth, contentHeight)));
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        framePool.FrameArrived += HandleFrameArrived;

        try
        {
            TryDisableCaptureBorder(session);
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                session.IsCursorCaptureEnabled = false;
            }
            session.StartCapture();
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally
        {
            framePool.FrameArrived -= HandleFrameArrived;
        }
    }

    private static GraphicsCaptureItem CreateItemForWindow(nint window)
    {
        IGraphicsCaptureItemInterop interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        nint itemPointer = interop.CreateForWindow(window, GraphicsCaptureItemGuid);

        try
        {
            return GraphicsCaptureItem.FromAbi(itemPointer);
        }
        finally
        {
            Marshal.Release(itemPointer);
        }
    }

    private static IDirect3DDevice CreateDevice()
    {
        int result = NativeMethods.D3D11CreateDevice(nint.Zero, D3DDriverTypeHardware, nint.Zero, D3D11CreateDeviceBgraSupport, nint.Zero, 0, D3D11SdkVersion, out nint nativeDevice, out _, out nint deviceContext);
        Marshal.ThrowExceptionForHR(result);
        nint dxgiDevice = nint.Zero;
        nint inspectableDevice = nint.Zero;

        try
        {
            Guid interfaceId = IdxgiDeviceGuid;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(nativeDevice, in interfaceId, out dxgiDevice));
            Marshal.ThrowExceptionForHR(NativeMethods.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out inspectableDevice));
            return MarshalInterface<IDirect3DDevice>.FromAbi(inspectableDevice);
        }
        finally
        {
            Release(inspectableDevice);
            Release(dxgiDevice);
            Release(deviceContext);
            Release(nativeDevice);
        }
    }

    private static void Release(nint value)
    {
        if (value != nint.Zero)
        {
            Marshal.Release(value);
        }
    }

    private static void TryDisableCaptureBorder(GraphicsCaptureSession session)
    {
        try
        {
            session.IsBorderRequired = false;
        }
        catch
        {
        }
    }

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    private interface IGraphicsCaptureItemInterop
    {
        nint CreateForWindow(nint window, in Guid interfaceId);

        nint CreateForMonitor(nint monitor, in Guid interfaceId);
    }

    private static partial class NativeMethods
    {
        [LibraryImport("d3d11.dll")]
        public static partial int D3D11CreateDevice(nint adapter, int driverType, nint software, uint flags, nint featureLevels, uint featureLevelCount, uint sdkVersion, out nint device, out uint featureLevel, out nint immediateContext);

        [LibraryImport("d3d11.dll")]
        public static partial int CreateDirect3D11DeviceFromDXGIDevice(nint dxgiDevice, out nint graphicsDevice);
    }
}
