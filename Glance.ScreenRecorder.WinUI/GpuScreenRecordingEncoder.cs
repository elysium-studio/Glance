using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage.Streams;
using WinRT;

namespace Glance.ScreenRecorder.WinUI;

internal sealed class GpuScreenRecordingEncoder :
    IDisposable
{
    private readonly ManualResetEvent closedEvent = new(false);
    private readonly Direct3DRecordingDevice recordingDevice;
    private readonly ManualResetEvent frameEvent = new(false);
    private readonly object frameLock = new();
    private readonly GraphicsCaptureItem item;
    private readonly MediaStreamSource mediaStreamSource;
    private readonly MediaTranscoder transcoder = new() { HardwareAccelerationEnabled = true };
    private readonly RecordingSource source;
    private readonly VideoStreamDescriptor videoDescriptor;
    private readonly WaitHandle[] waitHandles;
    private readonly TimeSpan frameDuration = TimeSpan.FromSeconds(1d / 30);
    private Direct3D11CaptureFrame? currentFrame;
    private Direct3D11CaptureFramePool? framePool;
    private GraphicsCaptureSession? session;
    private volatile bool disposed;
    private TimeSpan? firstSourceTimestamp;
    private TimeSpan? lastOutputTimestamp;
    private volatile bool paused;
    private volatile bool recording;
    private volatile bool resumePending;
    private TimeSpan timestampOffset;
    private SizeInt32 framePoolSize;

    public GpuScreenRecordingEncoder(RecordingSource source, bool includeCursor)
    {
        this.source = source;
        IsCursorCaptureEnabled = includeCursor;
        recordingDevice = Direct3DRecordingDevice.Create();
        item = Direct3DRecordingDevice.CreateCaptureItem(source);
        OutputWidth = MakeEven(Math.Max(2, source.Mode == ScreenRecordingMode.Window ? item.Size.Width : source.Bounds.Width));
        OutputHeight = MakeEven(Math.Max(2, source.Mode == ScreenRecordingMode.Window ? item.Size.Height : source.Bounds.Height));
        framePoolSize = item.Size;
        waitHandles = [closedEvent, frameEvent];

        VideoEncodingProperties inputProperties = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Bgra8, (uint)OutputWidth, (uint)OutputHeight);
        videoDescriptor = new VideoStreamDescriptor(inputProperties);
        mediaStreamSource = new MediaStreamSource(videoDescriptor)
        {
            BufferTime = TimeSpan.Zero
        };
        mediaStreamSource.Starting += HandleStarting;
        mediaStreamSource.SampleRequested += HandleSampleRequested;
    }

    public int OutputWidth { get; }

    public int OutputHeight { get; }

    public bool IsPaused => paused;

    public bool IsCursorCaptureEnabled { get; private set; }

    public async Task EncodeAsync(IRandomAccessStream stream, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        InitializeCapture();
        recording = true;

        MediaEncodingProfile profile = new();
        profile.Container.Subtype = MediaEncodingSubtypes.Mpeg4;
        profile.Video.Subtype = MediaEncodingSubtypes.H264;
        profile.Video.Width = (uint)OutputWidth;
        profile.Video.Height = (uint)OutputHeight;
        profile.Video.Bitrate = GetBitrate(OutputWidth, OutputHeight);
        profile.Video.FrameRate.Numerator = 30;
        profile.Video.FrameRate.Denominator = 1;
        profile.Video.PixelAspectRatio.Numerator = 1;
        profile.Video.PixelAspectRatio.Denominator = 1;

        PrepareTranscodeResult prepared = await transcoder.PrepareMediaStreamSourceTranscodeAsync(mediaStreamSource, stream, profile);

        if (!prepared.CanTranscode)
        {
            throw new InvalidOperationException($"The screen recording could not be prepared ({prepared.FailureReason}).");
        }

        using CancellationTokenRegistration registration = cancellationToken.Register(Stop);
        await prepared.TranscodeAsync();
    }

    public void Stop()
    {
        recording = false;
        _ = closedEvent.Set();
    }

    public bool SetPaused(bool value)
    {
        if (!recording || disposed || paused == value)
        {
            return paused == value;
        }

        paused = value;

        if (value)
        {
            lock (frameLock)
            {
                currentFrame?.Dispose();
                currentFrame = null;
                _ = frameEvent.Reset();
            }
        }
        else
        {
            resumePending = true;
        }

        return true;
    }

    public bool SetCursorCaptureEnabled(bool value)
    {
        if (disposed)
        {
            return false;
        }

        try
        {
            if (session is not null && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                session.IsCursorCaptureEnabled = value;
            }

            IsCursorCaptureEnabled = value;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Stop();
        mediaStreamSource.Starting -= HandleStarting;
        mediaStreamSource.SampleRequested -= HandleSampleRequested;

        framePool?.FrameArrived -= HandleFrameArrived;

        item.Closed -= HandleItemClosed;
        session?.Dispose();
        framePool?.Dispose();

        lock (frameLock)
        {
            currentFrame?.Dispose();
            currentFrame = null;
        }

        recordingDevice.Dispose();
        frameEvent.Dispose();
        closedEvent.Dispose();
    }

    private void InitializeCapture()
    {
        item.Closed += HandleItemClosed;
        framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(recordingDevice.Device,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            3,
            framePoolSize);
        framePool.FrameArrived += HandleFrameArrived;
        session = framePool.CreateCaptureSession(item);

        try
        {
            session.IsBorderRequired = false;
        }
        catch
        {
        }

        try
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                session.IsCursorCaptureEnabled = IsCursorCaptureEnabled;
            }
        }
        catch
        {
        }

        session.StartCapture();
    }

    private void HandleFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        Direct3D11CaptureFrame? frame = sender.TryGetNextFrame();

        if (frame is null)
        {
            return;
        }

        if (paused)
        {
            frame.Dispose();
            return;
        }

        lock (frameLock)
        {
            currentFrame?.Dispose();
            currentFrame = frame;
            _ = frameEvent.Set();
        }
    }

    private void HandleItemClosed(GraphicsCaptureItem sender, object args) => Stop();

    private void HandleStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
    {
        using EncodedFrame? frame = WaitForFrame();

        if (frame is not null)
        {
            args.Request.SetActualStartPosition(frame.Timestamp);
        }
    }

    private void HandleSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
    {
        if (!recording || disposed)
        {
            args.Request.Sample = null;
            return;
        }

        try
        {
            EncodedFrame? frame = WaitForFrame();

            if (frame is null)
            {
                args.Request.Sample = null;
                return;
            }

            MediaStreamSample sample = MediaStreamSample.CreateFromDirect3D11Surface(frame.Surface, frame.Timestamp);
            sample.Processed += (_, _) => frame.Dispose();
            args.Request.Sample = sample;
        }
        catch
        {
            args.Request.Sample = null;
            Stop();
        }
    }

    private EncodedFrame? WaitForFrame()
    {
        while (recording && !disposed)
        {
            WaitHandle signaled = waitHandles[WaitHandle.WaitAny(waitHandles)];

            if (signaled == closedEvent)
            {
                return null;
            }

            Direct3D11CaptureFrame? frame;

            lock (frameLock)
            {
                frame = currentFrame;
                currentFrame = null;
                _ = frameEvent.Reset();
            }

            if (frame is null)
            {
                continue;
            }

            if (paused)
            {
                frame.Dispose();
                continue;
            }

            using (frame)
            {
                EncodedFrame result = RenderFrame(frame, GetOutputTimestamp(frame.SystemRelativeTime));
                SizeInt32 contentSize = frame.ContentSize;

                if (contentSize.Width > 0 && contentSize.Height > 0 &&
                    (contentSize.Width != framePoolSize.Width || contentSize.Height != framePoolSize.Height))
                {
                    framePoolSize = contentSize;
                    framePool?.Recreate(recordingDevice.Device,
                        DirectXPixelFormat.B8G8R8A8UIntNormalized,
                        3,
                        framePoolSize);
                }

                return result;
            }
        }

        return null;
    }

    private EncodedFrame RenderFrame(Direct3D11CaptureFrame frame, TimeSpan timestamp)
    {
        CanvasRenderTarget renderTarget = new(recordingDevice.CanvasDevice,
            OutputWidth,
            OutputHeight,
            96,
            Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
            CanvasAlphaMode.Ignore);
        using CanvasBitmap bitmap = CanvasBitmap.CreateFromDirect3D11Surface(recordingDevice.CanvasDevice,
            frame.Surface,
            96,
            CanvasAlphaMode.Ignore);
        Rect sourceRectangle = GetSourceRectangle(frame);
        Rect destinationRectangle = GetDestinationRectangle(sourceRectangle.Width, sourceRectangle.Height);

        using (CanvasDrawingSession drawingSession = renderTarget.CreateDrawingSession())
        {
            drawingSession.Clear(Windows.UI.Color.FromArgb(255, 0, 0, 0));
            drawingSession.DrawImage(bitmap,
                destinationRectangle,
                sourceRectangle,
                1,
                CanvasImageInterpolation.Linear);
        }

        IDirect3DSurface surface = renderTarget.As<IDirect3DSurface>();
        return new EncodedFrame(surface, timestamp, renderTarget);
    }

    private TimeSpan GetOutputTimestamp(TimeSpan sourceTimestamp)
    {
        if (firstSourceTimestamp is null)
        {
            firstSourceTimestamp = sourceTimestamp;
            lastOutputTimestamp = TimeSpan.Zero;
            return TimeSpan.Zero;
        }

        if (resumePending)
        {
            TimeSpan desiredTimestamp = lastOutputTimestamp.GetValueOrDefault() + frameDuration;
            timestampOffset = sourceTimestamp - firstSourceTimestamp.Value - desiredTimestamp;
            resumePending = false;
        }

        TimeSpan timestamp = sourceTimestamp - firstSourceTimestamp.Value - timestampOffset;
        TimeSpan minimumTimestamp = lastOutputTimestamp.GetValueOrDefault() + TimeSpan.FromTicks(1);

        if (timestamp < minimumTimestamp)
        {
            timestamp = minimumTimestamp;
        }

        lastOutputTimestamp = timestamp;
        return timestamp;
    }

    private Rect GetSourceRectangle(Direct3D11CaptureFrame frame)
    {
        if (source.Mode != ScreenRecordingMode.Region)
        {
            return new Rect(0,
                0,
                Math.Clamp(frame.ContentSize.Width, 1, frame.Surface.Description.Width),
                Math.Clamp(frame.ContentSize.Height, 1, frame.Surface.Description.Height));
        }

        int relativeLeft = Math.Max(0, source.Bounds.Left - GetMonitorBounds(source.MonitorHandle).Left);
        int relativeTop = Math.Max(0, source.Bounds.Top - GetMonitorBounds(source.MonitorHandle).Top);
        int width = Math.Min(source.Bounds.Width, frame.Surface.Description.Width - relativeLeft);
        int height = Math.Min(source.Bounds.Height, frame.Surface.Description.Height - relativeTop);
        return new Rect(relativeLeft, relativeTop, Math.Max(1, width), Math.Max(1, height));
    }

    private Rect GetDestinationRectangle(double sourceWidth, double sourceHeight)
    {
        double scale = Math.Min(OutputWidth / sourceWidth, OutputHeight / sourceHeight);
        double width = sourceWidth * scale;
        double height = sourceHeight * scale;
        return new Rect((OutputWidth - width) / 2, (OutputHeight - height) / 2, width, height);
    }

    private static NativeRectangle GetMonitorBounds(nint monitor)
    {
        NativeMonitorInfo info = new() { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMonitorInfo>() };

        return !GetMonitorInfo(monitor, ref info)
            ? throw new InvalidOperationException("The selected display is no longer available.")
            : new NativeRectangle(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right, info.Monitor.Bottom);
    }

    private static uint GetBitrate(int width, int height) => (uint)Math.Clamp((long)width * height * 6, 4_000_000, 24_000_000);

    private static int MakeEven(int value) => value & ~1;

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref NativeMonitorInfo info);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private struct NativeMonitorInfo
    {
        public uint Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    private sealed class EncodedFrame(IDirect3DSurface surface,
        TimeSpan timestamp,
        CanvasRenderTarget renderTarget) :
        IDisposable
    {
        public IDirect3DSurface Surface { get; } = surface;

        public TimeSpan Timestamp { get; } = timestamp;

        public void Dispose()
        {
            Surface.Dispose();
            renderTarget.Dispose();
        }
    }
}
