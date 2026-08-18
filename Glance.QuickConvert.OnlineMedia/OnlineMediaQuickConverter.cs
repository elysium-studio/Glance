using Glance.Application.Abstractions;
using System.Diagnostics;

using Glance.QuickConvert.Tooling;

namespace Glance.QuickConvert.OnlineMedia;

public sealed class OnlineMediaQuickConverter(ModuleResourceTextLocalizer<OnlineMediaQuickConverterModule> localizer,
    QuickConvertToolProvider tools) :
    IGlanceQuickConverter
{
    private const string OutputPrefix = "__GLANCE_OUTPUT__";
    private readonly ModuleResourceTextLocalizer<OnlineMediaQuickConverterModule> localizer = localizer;

    public GlanceQuickConverterDescriptor Descriptor => new("QuickConvert.OnlineMedia",
        localizer.GetText("OnlineMediaConverterName"),
        localizer.GetText("OnlineMediaConverterDescription"));

    public GlanceQuickConverterMatch Match(GlanceContentContext context) =>
        context.Kind is GlanceContentKind.WebLink or GlanceContentKind.Text
            ? YtDlpUrlMatcher.Match(context.Content)
            : GlanceQuickConverterMatch.None;

    public IGlanceQuickConverterEditor CreateEditor(GlanceContentContext context) => new OnlineMediaQuickConverterEditor(context, localizer);

    public async Task<IReadOnlyList<GlanceQuickConversionResult>> ConvertAsync(GlanceQuickConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Options is not YtDlpConversionOptions options ||
            !YtDlpUrlMatcher.TryGetUri(request.Content.Content, out Uri source))
        {
            throw new ArgumentException("Online media conversion options were not supplied.", nameof(request));
        }

        Directory.CreateDirectory(options.DestinationFolder);
        Progress<double>? setupProgress = request.Progress is null
            ? null
            : new Progress<double>(value => request.Progress.Report(new GlanceQuickConversionProgress(GlanceQuickConversionStage.Setup,
                value,
                value >= 1)));
        QuickConvertToolPaths toolPaths = await tools.GetOnlineMediaToolsAsync(setupProgress, cancellationToken);
        ProcessStartInfo startInfo = new(toolPaths.YtDlpPath)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        foreach (string argument in YtDlpArguments.Create(options,
            Path.GetDirectoryName(toolPaths.FfmpegPath)!,
            toolPaths.DenoPath,
            source.AbsoluteUri))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("The online media converter could not be started.");
            }

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                TryStop(process);
                throw;
            }

            string output = await outputTask;
            string error = await errorTask;

            if (process.ExitCode != 0)
            {
                return [new GlanceQuickConversionResult(source.AbsoluteUri, null, false, CreateErrorMessage(error))];
            }

            string? outputPath = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => line.StartsWith(OutputPrefix, StringComparison.Ordinal))
                .Select(line => line[OutputPrefix.Length..])
                .LastOrDefault();
            return outputPath is null
                ? [new GlanceQuickConversionResult(source.AbsoluteUri, null, false, localizer.GetText("OutputFileMissing"))]
                : [new GlanceQuickConversionResult(source.AbsoluteUri, outputPath, true)];
        }
        finally
        {
            TryStop(process);
        }
    }

    private string CreateErrorMessage(string error)
    {
        if (ContainsAny(error, "private video", "video is private", "video unavailable", "video is unavailable", "removed by the uploader"))
        {
            return localizer.GetText("VideoUnavailable");
        }

        if (ContainsAny(error, "sign in", "login required", "confirm your age", "age-restricted", "cookies"))
        {
            return localizer.GetText("SignInRequired");
        }

        if (ContainsAny(error, "not available in your country", "not available in your region", "geo restricted", "geo-restricted"))
        {
            return localizer.GetText("RegionUnavailable");
        }

        if (ContainsAny(error, "unsupported url", "no suitable extractor"))
        {
            return localizer.GetText("LinkUnsupported");
        }

        if (ContainsAny(error, "requested format is not available", "requested format not available"))
        {
            return localizer.GetText("FormatUnavailable");
        }

        if (ContainsAny(error, "http error 429", "too many requests"))
        {
            return localizer.GetText("RequestsLimited");
        }

        if (ContainsAny(error, "http error 403", "forbidden"))
        {
            return localizer.GetText("DownloadRefused");
        }

        if (ContainsAny(error, "unable to download webpage", "timed out", "timeout", "temporary failure in name resolution", "no connection could be made"))
        {
            return localizer.GetText("ProviderUnavailable");
        }

        return localizer.GetText("DownloadFailed");
    }

    private static bool ContainsAny(string value, params string[] candidates) => candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));

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
}
