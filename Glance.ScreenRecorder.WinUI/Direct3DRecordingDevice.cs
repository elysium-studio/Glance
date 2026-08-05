using Microsoft.Graphics.Canvas;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace Glance.ScreenRecorder.WinUI;

internal sealed partial class Direct3DRecordingDevice :
    IDisposable
{
    private const uint BgraSupport = 0x20;
    private const uint VideoSupport = 0x800;
    private const uint SdkVersion = 7;
    private const int HardwareDriver = 1;
    private static readonly Guid CaptureItemGuid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid DxgiDeviceGuid = new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");

    private Direct3DRecordingDevice(IDirect3DDevice device)
    {
        Device = device;
        CanvasDevice = CanvasDevice.CreateFromDirect3D11Device(device);
    }

    public IDirect3DDevice Device { get; }

    public CanvasDevice CanvasDevice { get; }

    public static Direct3DRecordingDevice Create()
    {
        int result = NativeMethods.D3D11CreateDevice(nint.Zero,
            HardwareDriver,
            nint.Zero,
            BgraSupport | VideoSupport,
            nint.Zero,
            0,
            SdkVersion,
            out nint nativeDevice,
            out _,
            out nint deviceContext);
        Marshal.ThrowExceptionForHR(result);
        nint dxgiDevice = nint.Zero;
        nint inspectableDevice = nint.Zero;

        try
        {
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(nativeDevice, in DxgiDeviceGuid, out dxgiDevice));
            Marshal.ThrowExceptionForHR(NativeMethods.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out inspectableDevice));
            return new Direct3DRecordingDevice(MarshalInterface<IDirect3DDevice>.FromAbi(inspectableDevice));
        }
        finally
        {
            Release(inspectableDevice);
            Release(dxgiDevice);
            Release(deviceContext);
            Release(nativeDevice);
        }
    }

    public static GraphicsCaptureItem CreateCaptureItem(RecordingSource source)
    {
        IGraphicsCaptureItemInterop interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        nint itemPointer = source.WindowHandle != nint.Zero
            ? interop.CreateForWindow(source.WindowHandle, CaptureItemGuid)
            : interop.CreateForMonitor(source.MonitorHandle, CaptureItemGuid);

        try
        {
            return GraphicsCaptureItem.FromAbi(itemPointer);
        }
        finally
        {
            _ = Marshal.Release(itemPointer);
        }
    }

    public void Dispose()
    {
        CanvasDevice.Dispose();
        Device.Dispose();
    }

    private static void Release(nint value)
    {
        if (value != nint.Zero)
        {
            _ = Marshal.Release(value);
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
        public static partial int D3D11CreateDevice(nint adapter,
            int driverType,
            nint software,
            uint flags,
            nint featureLevels,
            uint featureLevelCount,
            uint sdkVersion,
            out nint device,
            out uint featureLevel,
            out nint immediateContext);

        [LibraryImport("d3d11.dll")]
        public static partial int CreateDirect3D11DeviceFromDXGIDevice(nint dxgiDevice, out nint graphicsDevice);
    }
}
