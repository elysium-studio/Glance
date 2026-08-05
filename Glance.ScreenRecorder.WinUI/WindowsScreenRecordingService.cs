using Glance.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Glance.ScreenRecorder.WinUI;

internal sealed class WindowsScreenRecordingService :
    IScreenRecordingService,
    IDisposable
{
    private const int ShowWindowHide = 0;
    private const int ShowWindowShowNoActivate = 4;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly string outputDirectory;
    private readonly ITextLocalizer localizer;
    private readonly ILogger<WindowsScreenRecordingService> logger;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private IReadOnlyList<ApplicationWindowState> hiddenApplicationWindows = [];
    private CancellationTokenSource? recordingCancellation;
    private RecordingBoundaryWindow? boundaryWindow;
    private RecordingSelectionWindow? selectionOverlay;
    private RecordingSource? activeSource;
    private GpuScreenRecordingEncoder? encoder;
    private IRandomAccessStream? outputStream;
    private string? outputPath;
    private Task? encodingTask;
    private Timer? elapsedTimer;
    private TimeSpan pausedDuration;
    private long pauseStartedTimestamp;
    private long recordingStartedTimestamp;
    private bool disposed;
    private RecordingAnimationFrame? pendingAnimationFrame;

    public WindowsScreenRecordingService(string outputDirectory,
        ITextLocalizer localizer,
        ILogger<WindowsScreenRecordingService> logger)
    {
        this.outputDirectory = outputDirectory;
        this.localizer = localizer;
        this.logger = logger;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _ = Directory.CreateDirectory(outputDirectory);
    }

    public event EventHandler<ScreenRecordingStateChangedEventArgs>? StateChanged;

    public ScreenRecordingState State { get; private set; }

    public bool IsPaused { get; private set; }

    public bool IsCursorCaptureEnabled { get; private set; }

    public IReadOnlyList<ScreenRecording> GetRecentRecordings(int maximumCount) => Directory.Exists(outputDirectory)
            ? [.. Directory.EnumerateFiles(outputDirectory, "*.mp4")
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.CreationTimeUtc)
                .Take(maximumCount)
                .Select(file => new ScreenRecording(file.FullName,
                    file.CreationTimeUtc,
                    TimeSpan.Zero,
                    0,
                    0,
                    ScreenRecordingMode.Display))]
            : [];

    public async Task<bool> StartAsync(ScreenRecordingMode mode,
        int countdownSeconds,
        bool includeCursor,
        string? windowName = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await operationGate.WaitAsync(cancellationToken);

        try
        {
            if (State is ScreenRecordingState.Selecting or ScreenRecordingState.CountingDown or ScreenRecordingState.Recording or ScreenRecordingState.Saving)
            {
                return false;
            }

            if (!Windows.Graphics.Capture.GraphicsCaptureSession.IsSupported())
            {
                SetState(ScreenRecordingState.Failed);
                return false;
            }

            hiddenApplicationWindows = GetVisibleApplicationWindows();
            HideApplicationWindows(hiddenApplicationWindows);
            _ = DwmFlush();
            SetState(ScreenRecordingState.Selecting);
            RecordingSelectionResult? selection = await SelectSourceAsync(mode, windowName);

            if (selection is null)
            {
                RestoreApplicationWindows();
                SetState(ScreenRecordingState.Idle);
                return false;
            }

            RecordingSource source = selection.Source;
            selectionOverlay = selection.Overlay;
            activeSource = source;
            recordingCancellation = new CancellationTokenSource();
            using CancellationTokenRegistration startupCancellation = cancellationToken.Register(recordingCancellation.Cancel);
            boundaryWindow = await RunOnUiThreadAsync(() =>
            {
                RecordingBoundaryWindow boundary = new(source, GetVirtualDesktopBounds(), dispatcherQueue, localizer);
                boundary.StopRequested += HandleBoundaryStopRequested;
                boundary.PauseToggleRequested += HandleBoundaryPauseToggleRequested;
                boundary.CursorCaptureToggleRequested += HandleBoundaryCursorCaptureToggleRequested;
                boundary.Show();
                return boundary;
            });

            for (int value = countdownSeconds; value > 0; value--)
            {
                SetState(ScreenRecordingState.CountingDown, countdown: value);
                int countdownValue = value;
                await RunOnUiThreadAsync(() => boundaryWindow.ShowCountdownAsync(countdownValue, recordingCancellation.Token));
            }

            recordingCancellation.Token.ThrowIfCancellationRequested();

            _ = Directory.CreateDirectory(outputDirectory);
            StorageFolder outputFolder = await StorageFolder.GetFolderFromPathAsync(outputDirectory);
            StorageFile outputFile = await outputFolder.CreateFileAsync(
                $"Glance recording {DateTime.Now:yyyy-MM-dd HH-mm-ss}.mp4",
                CreationCollisionOption.GenerateUniqueName);
            outputPath = outputFile.Path;
            outputStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
            encoder = new GpuScreenRecordingEncoder(source, includeCursor);
            IsCursorCaptureEnabled = includeCursor;
            IsPaused = false;
            pausedDuration = TimeSpan.Zero;
            pauseStartedTimestamp = 0;
            await RunOnUiThreadAsync(() =>
            {
                boundaryWindow.SetRecording();
                boundaryWindow.SetCursorCaptureEnabled(includeCursor);
            });
            recordingStartedTimestamp = Stopwatch.GetTimestamp();
            elapsedTimer = new Timer(HandleElapsedTimer, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(250));
            SetState(ScreenRecordingState.Recording);
            encodingTask = EncodeAndFinishAsync(encoder, outputStream, recordingCancellation.Token);
            _ = MonitorEncodingCompletionAsync(encodingTask);
            return true;
        }
        catch (OperationCanceledException)
        {
            await CleanupRecordingAsync(deleteIncompleteFile: true);
            SetState(ScreenRecordingState.Idle);
            return false;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Screen recording could not be started");
            await CleanupRecordingAsync(deleteIncompleteFile: true);
            SetState(ScreenRecordingState.Failed);
            return false;
        }
        finally
        {
            _ = operationGate.Release();
        }
    }

    public async Task<ScreenRecording?> StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await operationGate.WaitAsync(cancellationToken);
        string? completedPath = null;

        try
        {
            if (State != ScreenRecordingState.Recording || encoder is null || encodingTask is null)
            {
                return null;
            }

            TimeSpan duration = GetRecordingElapsed();
            SetState(ScreenRecordingState.Saving, duration);
            elapsedTimer?.Dispose();
            elapsedTimer = null;
            encoder.Stop();
            await encodingTask.WaitAsync(cancellationToken);
            _ = await outputStream!.FlushAsync();
            completedPath = outputPath!;
            RecordingSource completedSource = activeSource!;
            RecordingSelectionWindow reviewOverlay = selectionOverlay ??
                throw new InvalidOperationException("The recording selection surface is unavailable.");
            selectionOverlay = null;
            NativeRectangle reviewSourceBounds = boundaryWindow?.CurrentBounds ?? completedSource.Bounds;
            int width = encoder.OutputWidth;
            int height = encoder.OutputHeight;
            await CleanupRecordingAsync(deleteIncompleteFile: false, restoreApplicationWindows: false);
            RecordingReviewWindow? review = await reviewOverlay.ReviewAsync(completedPath,
                reviewSourceBounds,
                localizer);

            if (review is null)
            {
                TryDeleteFile(completedPath);
                RestoreApplicationWindows();
                SetState(ScreenRecordingState.Idle);
                return null;
            }

            ScreenRecording recording = new(completedPath,
                DateTimeOffset.Now,
                duration,
                width,
                height,
                completedSource.Mode);
            pendingAnimationFrame?.Overlay.Close();
            pendingAnimationFrame = new RecordingAnimationFrame(review);
            RestoreApplicationWindows();
            SetState(ScreenRecordingState.Completed, duration, recording: recording);
            return recording;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Screen recording could not be completed");
            await CleanupRecordingAsync(deleteIncompleteFile: true);
            TryDeleteFile(completedPath);
            SetState(ScreenRecordingState.Failed);
            return null;
        }
        finally
        {
            _ = operationGate.Release();
        }
    }

    public async Task<bool> SetPausedAsync(bool paused, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await operationGate.WaitAsync(cancellationToken);

        try
        {
            if (State != ScreenRecordingState.Recording || encoder is null)
            {
                return false;
            }

            if (IsPaused == paused)
            {
                return true;
            }

            if (!encoder.SetPaused(paused))
            {
                return false;
            }

            if (paused)
            {
                pauseStartedTimestamp = Stopwatch.GetTimestamp();
                _ = (elapsedTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan));
            }
            else
            {
                pausedDuration += Stopwatch.GetElapsedTime(pauseStartedTimestamp);
                pauseStartedTimestamp = 0;
                _ = (elapsedTimer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(250)));
            }

            IsPaused = paused;
            TimeSpan elapsed = GetRecordingElapsed();
            await RunOnUiThreadAsync(() => boundaryWindow?.SetPaused(paused));
            SetState(ScreenRecordingState.Recording, elapsed);
            return true;
        }
        finally
        {
            _ = operationGate.Release();
        }
    }

    public bool SetCursorCaptureEnabled(bool enabled)
    {
        ThrowIfDisposed();

        if (State != ScreenRecordingState.Recording || encoder is null || !encoder.SetCursorCaptureEnabled(enabled))
        {
            return false;
        }

        IsCursorCaptureEnabled = enabled;
        _ = RunOnUiThreadAsync(() => boundaryWindow?.SetCursorCaptureEnabled(enabled));
        return true;
    }

    public int CountMatchingWindows(string windowName) => FindWindowCandidates(EnumerateWindowCandidates(), windowName).Count;

    public bool TryOpen(ScreenRecording recording) => TryStartProcess(recording.FilePath);

    public bool TryReveal(ScreenRecording recording) => File.Exists(recording.FilePath) && TryStartProcess("explorer.exe", $"/select,\"{recording.FilePath}\"");

    public bool TryDelete(ScreenRecording recording)
    {
        try
        {
            if (File.Exists(recording.FilePath))
            {
                File.Delete(recording.FilePath);
            }

            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Screen recording file could not be deleted: {FilePath}", recording.FilePath);
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
        pendingAnimationFrame?.Overlay.Close();
        pendingAnimationFrame = null;
        selectionOverlay?.Close();
        selectionOverlay = null;
        encoder?.Stop();
        if (dispatcherQueue.HasThreadAccess)
        {
            if (boundaryWindow is not null)
            {
                boundaryWindow.StopRequested -= HandleBoundaryStopRequested;
                boundaryWindow.PauseToggleRequested -= HandleBoundaryPauseToggleRequested;
                boundaryWindow.CursorCaptureToggleRequested -= HandleBoundaryCursorCaptureToggleRequested;
                boundaryWindow.Dispose();
            }

            boundaryWindow = null;
            CleanupRecording(deleteIncompleteFile: true);
        }
        else
        {
            _ = dispatcherQueue.TryEnqueue(() =>
            {
                if (boundaryWindow is not null)
                {
                    boundaryWindow.StopRequested -= HandleBoundaryStopRequested;
                    boundaryWindow.PauseToggleRequested -= HandleBoundaryPauseToggleRequested;
                    boundaryWindow.CursorCaptureToggleRequested -= HandleBoundaryCursorCaptureToggleRequested;
                    boundaryWindow.Dispose();
                }

                boundaryWindow = null;
                CleanupRecording(deleteIncompleteFile: true);
            });
        }

        operationGate.Dispose();
    }

    internal RecordingAnimationFrame? TakeAnimationFrame()
    {
        RecordingAnimationFrame? frame = pendingAnimationFrame;
        pendingAnimationFrame = null;
        return frame;
    }

    private async Task<RecordingSelectionResult?> SelectSourceAsync(ScreenRecordingMode mode, string? windowName)
    {
        RecordingSource? automaticSource = null;

        if (mode == ScreenRecordingMode.Window && !string.IsNullOrWhiteSpace(windowName))
        {
            RecordingSelectionCandidate? match = FindWindowCandidates(EnumerateWindowCandidates(), windowName).SingleOrDefault();

            if (match is null)
            {
                return null;
            }

            automaticSource = new RecordingSource(mode, match.Bounds, match.WindowHandle, match.MonitorHandle);
        }

        IReadOnlyList<RecordingSelectionCandidate> candidates = mode == ScreenRecordingMode.Window
            ? EnumerateWindowCandidates()
            : EnumerateDisplayCandidates();
        return await RecordingSelectionWindow.SelectAsync(mode,
            candidates,
            GetVirtualDesktopBounds(),
            localizer,
            dispatcherQueue,
            automaticSource);
    }

    private async Task EncodeAndFinishAsync(GpuScreenRecordingEncoder activeEncoder,
        IRandomAccessStream stream,
        CancellationToken cancellationToken)
    {
        try
        {
            await activeEncoder.EncodeAsync(stream, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task MonitorEncodingCompletionAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
        }

        if (!disposed && State == ScreenRecordingState.Recording)
        {
            _ = dispatcherQueue.TryEnqueue(async () => await StopAsync());
        }
    }

    private void HandleElapsedTimer(object? state)
    {
        if (State == ScreenRecordingState.Recording && !IsPaused && recordingStartedTimestamp != 0)
        {
            SetState(ScreenRecordingState.Recording, GetRecordingElapsed());
        }
    }

    private async void HandleBoundaryStopRequested(object? sender, EventArgs args)
    {
        if (State is ScreenRecordingState.Selecting or ScreenRecordingState.CountingDown)
        {
            recordingCancellation?.Cancel();
            return;
        }

        if (State == ScreenRecordingState.Recording)
        {
            _ = await StopAsync();
        }
    }

    private async void HandleBoundaryPauseToggleRequested(object? sender, EventArgs args) => _ = await SetPausedAsync(!IsPaused);

    private void HandleBoundaryCursorCaptureToggleRequested(object? sender, EventArgs args) => _ = SetCursorCaptureEnabled(!IsCursorCaptureEnabled);

    private void SetState(ScreenRecordingState state,
        TimeSpan elapsed = default,
        int countdown = 0,
        ScreenRecording? recording = null)
    {
        State = state;
        StateChanged?.Invoke(this, new ScreenRecordingStateChangedEventArgs(state, elapsed, countdown, recording, IsPaused));
    }

    private async Task CleanupRecordingAsync(bool deleteIncompleteFile, bool restoreApplicationWindows = true)
    {
        RecordingBoundaryWindow? boundary = boundaryWindow;
        boundaryWindow = null;

        if (boundary is not null)
        {
            boundary.StopRequested -= HandleBoundaryStopRequested;
            boundary.PauseToggleRequested -= HandleBoundaryPauseToggleRequested;
            boundary.CursorCaptureToggleRequested -= HandleBoundaryCursorCaptureToggleRequested;
            await RunOnUiThreadAsync(boundary.Dispose);
        }

        CleanupRecording(deleteIncompleteFile, restoreApplicationWindows);
    }

    private void CleanupRecording(bool deleteIncompleteFile, bool restoreApplicationWindows = true)
    {
        elapsedTimer?.Dispose();
        elapsedTimer = null;
        encoder?.Dispose();
        encoder = null;
        outputStream?.Dispose();
        outputStream = null;
        recordingCancellation?.Dispose();
        recordingCancellation = null;
        encodingTask = null;
        recordingStartedTimestamp = 0;
        pauseStartedTimestamp = 0;
        pausedDuration = TimeSpan.Zero;
        IsPaused = false;
        IsCursorCaptureEnabled = false;
        activeSource = null;
        selectionOverlay?.Close();
        selectionOverlay = null;
        if (restoreApplicationWindows)
        {
            RestoreApplicationWindows();
        }

        if (deleteIncompleteFile && outputPath is not null)
        {
            try
            {
                File.Delete(outputPath);
            }
            catch
            {
            }
        }

        outputPath = null;
    }

    private TimeSpan GetRecordingElapsed()
    {
        if (recordingStartedTimestamp == 0)
        {
            return TimeSpan.Zero;
        }

        TimeSpan currentPause = IsPaused && pauseStartedTimestamp != 0
            ? Stopwatch.GetElapsedTime(pauseStartedTimestamp)
            : TimeSpan.Zero;
        TimeSpan elapsed = Stopwatch.GetElapsedTime(recordingStartedTimestamp) - pausedDuration - currentPause;
        return elapsed > TimeSpan.Zero ? elapsed : TimeSpan.Zero;
    }

    private static void TryDeleteFile(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }

    private void RestoreApplicationWindows()
    {
        IReadOnlyList<ApplicationWindowState> windows = hiddenApplicationWindows;
        hiddenApplicationWindows = [];

        foreach (ApplicationWindowState window in windows)
        {
            _ = ShowWindow(window.Handle, ShowWindowShowNoActivate);
        }

        _ = DwmFlush();
    }

    private static IReadOnlyList<ApplicationWindowState> GetVisibleApplicationWindows()
    {
        uint processId = (uint)Environment.ProcessId;
        List<ApplicationWindowState> windows = [];
        _ = EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out uint windowProcessId);

            if (windowProcessId == processId && IsWindowVisible(window))
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
            _ = ShowWindow(window.Handle, ShowWindowHide);
        }
    }

    private Task RunOnUiThreadAsync(Action action)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
                _ = completion.TrySetResult();
            }
            catch (Exception exception)
            {
                _ = completion.TrySetException(exception);
            }
        }))
        {
            _ = completion.TrySetException(new InvalidOperationException("The Glance UI thread is unavailable."));
        }

        return completion.Task;
    }

    private Task<T> RunOnUiThreadAsync<T>(Func<T> action)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            return Task.FromResult(action());
        }

        TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                _ = completion.TrySetResult(action());
            }
            catch (Exception exception)
            {
                _ = completion.TrySetException(exception);
            }
        }))
        {
            _ = completion.TrySetException(new InvalidOperationException("The Glance UI thread is unavailable."));
        }

        return completion.Task;
    }

    private Task RunOnUiThreadAsync(Func<Task> action)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            return action();
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await action();
                _ = completion.TrySetResult();
            }
            catch (Exception exception)
            {
                _ = completion.TrySetException(exception);
            }
        }))
        {
            _ = completion.TrySetException(new InvalidOperationException("The Glance UI thread is unavailable."));
        }

        return completion.Task;
    }

    private static IReadOnlyList<RecordingSelectionCandidate> EnumerateWindowCandidates()
    {
        List<RecordingSelectionCandidate> candidates = [];
        uint currentProcessId = (uint)Environment.ProcessId;
        _ = EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out uint processId);

            if (processId == currentProcessId || !IsWindowVisible(window) || IsIconic(window))
            {
                return true;
            }


            if (DwmGetWindowAttribute(window, 9, out NativeRect rectangle, Marshal.SizeOf<NativeRect>()) != 0 &&
                !GetWindowRect(window, out rectangle))
            {
                return true;
            }

            NativeRectangle bounds = new(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);

            if (bounds.Width < 80 || bounds.Height < 60)
            {
                return true;
            }

            nint monitor = MonitorFromWindow(window, 2);
            candidates.Add(new RecordingSelectionCandidate(bounds, window, monitor));
            return true;
        }, nint.Zero);
        return candidates;
    }

    private static IReadOnlyList<RecordingSelectionCandidate> EnumerateDisplayCandidates()
    {
        List<RecordingSelectionCandidate> candidates = [];
        _ = EnumDisplayMonitors(nint.Zero, nint.Zero, (monitor, _, _, _) =>
        {
            NativeMonitorInfo info = new() { Size = (uint)Marshal.SizeOf<NativeMonitorInfo>() };

            if (GetMonitorInfo(monitor, ref info))
            {
                candidates.Add(new RecordingSelectionCandidate(new NativeRectangle(info.Monitor.Left,
                    info.Monitor.Top,
                    info.Monitor.Right,
                    info.Monitor.Bottom),
                    nint.Zero,
                    monitor));
            }

            return true;
        }, nint.Zero);
        return candidates;
    }

    private static IReadOnlyList<RecordingSelectionCandidate> FindWindowCandidates(IReadOnlyList<RecordingSelectionCandidate> candidates,
        string query)
    {
        string normalized = query.Trim();
        return [.. candidates.Where(candidate =>
        {
            int length = GetWindowTextLength(candidate.WindowHandle);
            StringBuilder title = new(length + 1);
            _ = GetWindowText(candidate.WindowHandle, title, title.Capacity);
            return title.ToString().Contains(normalized, StringComparison.OrdinalIgnoreCase);
        })];
    }

    private static bool TryGetWindowBounds(nint window, out NativeRectangle bounds)
    {
        if (DwmGetWindowAttribute(window, 9, out NativeRect rectangle, Marshal.SizeOf<NativeRect>()) == 0 &&
            rectangle.Right > rectangle.Left && rectangle.Bottom > rectangle.Top)
        {
            bounds = new NativeRectangle(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);
            return true;
        }

        bounds = default;
        return false;
    }

    private static NativeRectangle GetVirtualDesktopBounds()
    {
        int left = GetSystemMetrics(76);
        int top = GetSystemMetrics(77);
        return new NativeRectangle(left,
            top,
            left + GetSystemMetrics(78),
            top + GetSystemMetrics(79));
    }

    private static bool TryStartProcess(string fileName, string? arguments = null)
    {
        try
        {
            _ = Process.Start(new ProcessStartInfo(fileName, arguments ?? string.Empty) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    private delegate bool EnumWindowsCallback(nint window, nint parameter);

    private delegate bool EnumDisplayMonitorsCallback(nint monitor, nint deviceContext, nint rectangle, nint parameter);

    private readonly record struct ApplicationWindowState(nint Handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(nint deviceContext, nint clip, EnumDisplayMonitorsCallback callback, nint parameter);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint window, int attribute, out NativeRect value, int valueSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int valueSize);

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint window, uint flags);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint window, StringBuilder text, int maximumCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(nint window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect rectangle);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref NativeMonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct NativeMonitorInfo
    {
        public uint Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }
}
