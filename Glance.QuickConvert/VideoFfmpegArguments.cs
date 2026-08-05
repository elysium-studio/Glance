using System.Globalization;

namespace Glance.QuickConvert;

public static class VideoFfmpegArguments
{
    public static IReadOnlyList<string> Create(VideoConversionOptions options)
    {
        List<string> arguments = [];
        string format = options.Format.ToLowerInvariant();

        if (IsAudioOnly(format))
        {
            arguments.Add("-vn");
            AddAudioArguments(arguments, format, options.Quality);
            return arguments;
        }

        string scale = CreateScaleFilter(options);

        if (format == "gif")
        {
            arguments.Add("-filter_complex");
            arguments.Add($"[0:v]fps=15,{scale}:flags=lanczos,split[gif_a][gif_b];[gif_a]palettegen=max_colors=192[gif_p];[gif_b][gif_p]paletteuse=dither=sierra2_4a");
            arguments.Add("-an");
            arguments.Add("-loop");
            arguments.Add("0");
            return arguments;
        }

        arguments.Add("-vf");
        arguments.Add(scale);
        AddVideoArguments(arguments, format, options.Quality);
        return arguments;
    }

    public static bool IsAudioOnly(string format) => format.Equals("mp3", StringComparison.OrdinalIgnoreCase) ||
        format.Equals("m4a", StringComparison.OrdinalIgnoreCase) ||
        format.Equals("wav", StringComparison.OrdinalIgnoreCase) ||
        format.Equals("flac", StringComparison.OrdinalIgnoreCase) ||
        format.Equals("ogg", StringComparison.OrdinalIgnoreCase);

    private static string CreateScaleFilter(VideoConversionOptions options) => options.ScaleMode switch
    {
        VideoScaleMode.Percentage => $"scale=trunc(iw*{(options.Percentage / 100).ToString("0.####", CultureInfo.InvariantCulture)}/2)*2:trunc(ih*{(options.Percentage / 100).ToString("0.####", CultureInfo.InvariantCulture)}/2)*2",
        VideoScaleMode.FitWithin => $"scale={options.Width}:{options.Height}:force_original_aspect_ratio=decrease:force_divisible_by=2",
        _ => "scale=trunc(iw/2)*2:trunc(ih/2)*2"
    };

    private static void AddVideoArguments(List<string> arguments,
        string format,
        VideoConversionQuality quality)
    {
        (string videoBitrate, string audioBitrate, string vp9Crf, string mpegQuality) = quality switch
        {
            VideoConversionQuality.Smaller => ("2M", "128k", "36", "7"),
            VideoConversionQuality.High => ("8M", "256k", "24", "2"),
            _ => ("4M", "192k", "30", "4")
        };

        switch (format)
        {
            case "webm":
            case "mkv":
                arguments.AddRange(["-c:v", "libvpx-vp9", "-crf", vp9Crf, "-b:v", "0", "-c:a", "libopus", "-b:a", audioBitrate]);
                break;
            case "avi":
                arguments.AddRange(["-c:v", "mpeg4", "-q:v", mpegQuality, "-c:a", "libmp3lame", "-b:a", audioBitrate]);
                break;
            case "mov":
                arguments.AddRange(["-c:v", "mpeg4", "-q:v", mpegQuality, "-c:a", "aac", "-b:a", audioBitrate]);
                break;
            default:
                arguments.AddRange(["-c:v", "libopenh264", "-b:v", videoBitrate, "-maxrate", videoBitrate, "-bufsize", "8M", "-c:a", "aac", "-b:a", audioBitrate, "-movflags", "+faststart"]);
                break;
        }
    }

    private static void AddAudioArguments(List<string> arguments,
        string format,
        VideoConversionQuality quality)
    {
        string bitrate = quality switch
        {
            VideoConversionQuality.Smaller => "128k",
            VideoConversionQuality.High => "256k",
            _ => "192k"
        };

        switch (format)
        {
            case "wav":
                arguments.AddRange(["-c:a", "pcm_s16le"]);
                break;
            case "flac":
                arguments.AddRange(["-c:a", "flac"]);
                break;
            case "ogg":
                arguments.AddRange(["-c:a", "libvorbis", "-q:a", quality == VideoConversionQuality.High ? "7" : quality == VideoConversionQuality.Smaller ? "3" : "5"]);
                break;
            case "m4a":
                arguments.AddRange(["-c:a", "aac", "-b:a", bitrate]);
                break;
            default:
                arguments.AddRange(["-c:a", "libmp3lame", "-b:a", bitrate]);
                break;
        }
    }
}
