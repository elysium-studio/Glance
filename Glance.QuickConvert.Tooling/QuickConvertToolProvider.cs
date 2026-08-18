using Glance.Application.Abstractions;
using System.IO.Compression;

namespace Glance.QuickConvert.Tooling;

public sealed class QuickConvertToolProvider(IBackgroundDownloadManager downloads) :
    IDisposable
{
    private static readonly TimeSpan ytDlpRefreshInterval = TimeSpan.FromDays(1);
    private static readonly Uri ffmpegSource = new("https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n8.1-latest-win64-lgpl-8.1.zip");
    private static readonly Uri ytDlpSource = new("https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/latest/download/yt-dlp.exe");
    private static readonly Uri denoSource = new("https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip");
    private readonly SemaphoreSlim synchronization = new(1, 1);
    private readonly object progressSynchronization = new();
    private readonly Dictionary<string, double> setupProgress = new(StringComparer.OrdinalIgnoreCase);
    private IProgress<double>? progress;
    private readonly string toolsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Glance",
        "Tools",
        "QuickConvert");

    public async Task<QuickConvertToolPaths> GetVideoToolsAsync(IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        await synchronization.WaitAsync(cancellationToken);

        try
        {
            bool requiresSetup = !HasFfmpeg();

            if (requiresSetup)
            {
                BeginSetup(["quick-convert:ffmpeg"], progress);
            }

            await EnsureFfmpegAsync(cancellationToken);

            if (requiresSetup)
            {
                CompleteSetup();
            }

            return CreatePaths();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new GlanceQuickConverterSetupException(exception);
        }
        finally
        {
            _ = synchronization.Release();
        }
    }

    public async Task<QuickConvertToolPaths> GetOnlineMediaToolsAsync(IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        await synchronization.WaitAsync(cancellationToken);

        try
        {
            List<string> requiredDownloads = [];

            if (!HasFfmpeg())
            {
                requiredDownloads.Add("quick-convert:ffmpeg");
            }

            if (RequiresYtDlpRefresh())
            {
                requiredDownloads.Add("quick-convert:yt-dlp");
            }

            if (!File.Exists(CreatePaths().DenoPath))
            {
                requiredDownloads.Add("quick-convert:deno");
            }

            if (requiredDownloads.Count > 0)
            {
                BeginSetup(requiredDownloads, progress);
            }

            await Task.WhenAll(EnsureFfmpegAsync(cancellationToken),
                EnsureYtDlpAsync(cancellationToken),
                EnsureDenoAsync(cancellationToken));

            if (requiredDownloads.Count > 0)
            {
                CompleteSetup();
            }

            return CreatePaths();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new GlanceQuickConverterSetupException(exception);
        }
        finally
        {
            _ = synchronization.Release();
        }
    }

    public void Dispose() => synchronization.Dispose();

    private QuickConvertToolPaths CreatePaths()
    {
        string ffmpegDirectory = Path.Combine(toolsDirectory, "ffmpeg");
        return new QuickConvertToolPaths(Path.Combine(ffmpegDirectory, "ffmpeg.exe"),
            Path.Combine(ffmpegDirectory, "ffprobe.exe"),
            Path.Combine(toolsDirectory, "online-media", "yt-dlp.exe"),
            Path.Combine(toolsDirectory, "online-media", "deno.exe"));
    }

    private async Task EnsureFfmpegAsync(CancellationToken cancellationToken)
    {
        QuickConvertToolPaths paths = CreatePaths();

        if (File.Exists(paths.FfmpegPath) && File.Exists(paths.FfprobePath))
        {
            return;
        }

        string archivePath = Path.Combine(toolsDirectory, "downloads", "ffmpeg.zip");
        await DownloadAsync("quick-convert:ffmpeg", ffmpegSource, archivePath, cancellationToken);
        string stagingDirectory = CreateStagingDirectory();

        try
        {
            ZipFile.ExtractToDirectory(archivePath, stagingDirectory, true);
            string sourceDirectory = Path.GetDirectoryName(Directory.GetFiles(stagingDirectory, "ffmpeg.exe", SearchOption.AllDirectories).Single())!;
            string destinationDirectory = Path.Combine(toolsDirectory, "ffmpeg");
            Directory.CreateDirectory(destinationDirectory);

            foreach (string sourcePath in Directory.GetFiles(sourceDirectory))
            {
                string extension = Path.GetExtension(sourcePath);

                if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(sourcePath, Path.Combine(destinationDirectory, Path.GetFileName(sourcePath)), true);
                }
            }
        }
        finally
        {
            Directory.Delete(stagingDirectory, true);
            File.Delete(archivePath);
        }
    }

    private bool HasFfmpeg()
    {
        QuickConvertToolPaths paths = CreatePaths();
        return File.Exists(paths.FfmpegPath) && File.Exists(paths.FfprobePath);
    }

    private async Task EnsureYtDlpAsync(CancellationToken cancellationToken)
    {
        if (RequiresYtDlpRefresh())
        {
            string path = CreatePaths().YtDlpPath;
            await DownloadAsync("quick-convert:yt-dlp", ytDlpSource, path, cancellationToken);
            File.WriteAllText(CreateYtDlpRefreshPath(), DateTimeOffset.UtcNow.ToString("O"));
        }
    }

    private bool RequiresYtDlpRefresh()
    {
        QuickConvertToolPaths paths = CreatePaths();
        string refreshPath = CreateYtDlpRefreshPath();
        return !File.Exists(paths.YtDlpPath) || !File.Exists(refreshPath) || DateTime.UtcNow - File.GetLastWriteTimeUtc(refreshPath) >= ytDlpRefreshInterval;
    }

    private string CreateYtDlpRefreshPath() => Path.Combine(toolsDirectory, "online-media", "yt-dlp.refresh");

    private async Task EnsureDenoAsync(CancellationToken cancellationToken)
    {
        string path = CreatePaths().DenoPath;

        if (File.Exists(path))
        {
            return;
        }

        string archivePath = Path.Combine(toolsDirectory, "downloads", "deno.zip");
        await DownloadAsync("quick-convert:deno", denoSource, archivePath, cancellationToken);
        string stagingDirectory = CreateStagingDirectory();

        try
        {
            ZipFile.ExtractToDirectory(archivePath, stagingDirectory, true);
            string sourcePath = Directory.GetFiles(stagingDirectory, "deno.exe", SearchOption.AllDirectories).Single();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(sourcePath, path, true);
        }
        finally
        {
            Directory.Delete(stagingDirectory, true);
            File.Delete(archivePath);
        }
    }

    private async Task DownloadAsync(string id,
        Uri source,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        EventHandler<BackgroundDownloadChangedEventArgs> handler = (_, args) =>
        {
            if (string.Equals(args.Download.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                ReportProgress(id, args.Download.Progress);
            }
        };
        downloads.DownloadChanged += handler;

        try
        {
            BackgroundDownloadSnapshot snapshot = downloads.Enqueue(new BackgroundDownloadRequest(id, source, destinationPath));

            if (snapshot.Status != BackgroundDownloadStatus.Completed)
            {
                snapshot = await downloads.WaitForCompletionAsync(id, cancellationToken);
            }

            if (snapshot.Status == BackgroundDownloadStatus.Cancelled)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (snapshot.Status != BackgroundDownloadStatus.Completed)
            {
                throw new IOException(snapshot.ErrorMessage ?? "The conversion tools could not be downloaded.");
            }

            ReportProgress(id, 1);
        }
        finally
        {
            downloads.DownloadChanged -= handler;
        }
    }

    private void BeginSetup(IEnumerable<string> downloadIds,
        IProgress<double>? progress)
    {
        lock (progressSynchronization)
        {
            setupProgress.Clear();
            this.progress = progress;

            foreach (string id in downloadIds)
            {
                setupProgress[id] = 0;
            }
        }

        progress?.Report(0);
    }

    private void ReportProgress(string id,
        double progress)
    {
        double overallProgress;

        lock (progressSynchronization)
        {
            if (!setupProgress.ContainsKey(id))
            {
                return;
            }

            setupProgress[id] = progress;
            overallProgress = setupProgress.Values.Average();
        }

        this.progress?.Report(overallProgress);
    }

    private void CompleteSetup()
    {
        IProgress<double>? completionProgress;

        lock (progressSynchronization)
        {
            setupProgress.Clear();
            completionProgress = progress;
            progress = null;
        }

        completionProgress?.Report(1);
    }

    private string CreateStagingDirectory()
    {
        string path = Path.Combine(toolsDirectory, "staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

public sealed record QuickConvertToolPaths(string FfmpegPath,
    string FfprobePath,
    string YtDlpPath,
    string DenoPath);
