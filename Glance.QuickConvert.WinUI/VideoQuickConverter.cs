using Glance.Application.Abstractions;
using System.Diagnostics;

namespace Glance.QuickConvert.WinUI;

public sealed class VideoQuickConverter(ModuleResourceTextLocalizer<QuickConvertModule> localizer) :
    IGlanceQuickConverter
{
    private static readonly HashSet<string> supportedExtensions =
    [
        with(StringComparer.OrdinalIgnoreCase),
        ".3g2", ".3gp", ".avi", ".flv", ".m2ts", ".m4v", ".mkv", ".mov", ".mp4", ".mpeg", ".mpg", ".mts", ".ogv", ".ts", ".webm", ".wmv"
    ];
    private readonly ModuleResourceTextLocalizer<QuickConvertModule> localizer = localizer;

    public GlanceQuickConverterDescriptor Descriptor => new("QuickConvert.Video",
        localizer.GetText("VideoConverterName"),
        localizer.GetText("VideoConverterDescription"));

    public bool CanConvert(IReadOnlyList<GlanceStorageItem> items) => items.Count > 0 && items.All(item => !item.IsFolder && supportedExtensions.Contains(Path.GetExtension(item.Path)));

    public IGlanceQuickConverterEditor CreateEditor(IReadOnlyList<GlanceStorageItem> items) => new VideoQuickConverterEditor(localizer);

    public async Task<IReadOnlyList<GlanceQuickConversionResult>> ConvertAsync(GlanceQuickConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Options is not VideoConversionOptions options)
        {
            throw new ArgumentException("Video conversion options were not supplied.", nameof(request));
        }

        string executablePath = FindExecutable();
        List<GlanceQuickConversionResult> results = [];

        foreach (GlanceStorageItem item in request.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                string outputPath = await ConvertAsync(executablePath, item.Path, options, cancellationToken);
                results.Add(new GlanceQuickConversionResult(item.Path, outputPath, true));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                results.Add(new GlanceQuickConversionResult(item.Path, null, false, exception.Message));
            }
        }

        return results;
    }

    private static async Task<string> ConvertAsync(string executablePath,
        string sourcePath,
        VideoConversionOptions options,
        CancellationToken cancellationToken)
    {
        string outputPath = QuickConvertFileName.Create(sourcePath, options.Format, File.Exists);
        ProcessStartInfo startInfo = new(executablePath)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-nostdin");
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(sourcePath);

        foreach (string argument in VideoFfmpegArguments.Create(options))
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add(outputPath);
        using Process process = new() { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("FFmpeg could not be started.");
            }

            Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                TryStop(process);
                throw;
            }

            string error = await errorTask;
            _ = await outputTask;

            return process.ExitCode != 0 ? throw new InvalidOperationException(CreateErrorMessage(error)) : outputPath;
        }
        catch
        {
            TryStop(process);

            TryDelete(outputPath);

            throw;
        }
    }

    private static string FindExecutable()
    {
        string assemblyDirectory = Path.GetDirectoryName(typeof(VideoQuickConverter).Assembly.Location)!;
        string[] candidates =
        [
            Path.Combine(assemblyDirectory, "ffmpeg.exe"),
            Path.Combine(assemblyDirectory, "ffmpeg", "win-x64", "ffmpeg.exe"),
            Path.Combine(AppContext.BaseDirectory, "ffmpeg", "win-x64", "ffmpeg.exe")
        ];

        return candidates.FirstOrDefault(File.Exists) ??
            throw new FileNotFoundException("The video conversion engine is unavailable.");
    }

    private static string CreateErrorMessage(string error)
    {
        string[] lines = error.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.LastOrDefault() ?? "The video could not be converted.";
    }

    private static void TryStop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
