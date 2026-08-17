namespace Glance.QuickConvert.OnlineMedia;

public static class YtDlpArguments
{
    private static readonly HashSet<string> audioFormats =
    [
        with(StringComparer.OrdinalIgnoreCase),
        "flac", "m4a", "mp3", "opus", "wav"
    ];
    private static readonly HashSet<string> videoFormats =
    [
        with(StringComparer.OrdinalIgnoreCase),
        "mkv", "mp4", "webm"
    ];

    public static IReadOnlyList<string> Create(YtDlpConversionOptions options,
        string ffmpegDirectory,
        string denoPath,
        string source)
    {
        string format = options.Format.ToLowerInvariant();

        if (!audioFormats.Contains(format) && !videoFormats.Contains(format))
        {
            throw new ArgumentException("The selected output format is not supported.", nameof(options));
        }

        List<string> arguments =
        [
            "--ignore-config",
            "--no-playlist",
            "--newline",
            "--ffmpeg-location", ffmpegDirectory,
            "--js-runtimes", $"deno:{denoPath}",
            "--paths", options.DestinationFolder,
            "--output", "%(title).200B [%(id)s].%(ext)s",
            "--print", "after_move:__GLANCE_OUTPUT__%(filepath)s"
        ];

        if (audioFormats.Contains(format))
        {
            arguments.Add("--extract-audio");
            arguments.Add("--audio-format");
            arguments.Add(format);
            arguments.Add("--audio-quality");
            arguments.Add("0");
        }
        else
        {
            arguments.Add("--format");
            arguments.Add(options.MaximumHeight > 0
                ? $"bv*[height<={options.MaximumHeight}]+ba/b[height<={options.MaximumHeight}]"
                : "bv*+ba/b");
            arguments.Add("--recode-video");
            arguments.Add(format);
        }

        arguments.Add(source);
        return arguments;
    }
}
