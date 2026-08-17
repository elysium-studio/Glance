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

    private static string CreateErrorMessage(string error) => error
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .LastOrDefault(line => line.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
        ?? "The online media could not be downloaded.";

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
