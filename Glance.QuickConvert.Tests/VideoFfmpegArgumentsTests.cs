namespace Glance.QuickConvert.Tests;

public sealed class VideoFfmpegArgumentsTests
{
    [Fact]
    public void AudioOutputDisablesVideoAndDoesNotScale()
    {
        VideoConversionOptions options = new("mp3", VideoScaleMode.FitWithin, 100, 1920, 1080, VideoConversionQuality.Balanced);

        IReadOnlyList<string> arguments = VideoFfmpegArguments.Create(options);

        Assert.Contains("-vn", arguments);
        Assert.DoesNotContain("-vf", arguments);
        Assert.Contains("libmp3lame", arguments);
    }

    [Fact]
    public void GifUsesOnePassPaletteFilter()
    {
        VideoConversionOptions options = new("gif", VideoScaleMode.Original, 100, 1920, 1080, VideoConversionQuality.Balanced);

        IReadOnlyList<string> arguments = VideoFfmpegArguments.Create(options);

        Assert.Contains("-filter_complex", arguments);
        Assert.Contains(arguments, argument => argument.Contains("palettegen", StringComparison.Ordinal));
        Assert.Contains(arguments, argument => argument.Contains("paletteuse", StringComparison.Ordinal));
    }

    [Fact]
    public void FitWithinPreservesAspectRatioAndEvenDimensions()
    {
        VideoConversionOptions options = new("mp4", VideoScaleMode.FitWithin, 100, 1280, 720, VideoConversionQuality.High);

        IReadOnlyList<string> arguments = VideoFfmpegArguments.Create(options);
        int filterIndex = Array.IndexOf([.. arguments], "-vf");
        string filter = arguments[filterIndex + 1];

        Assert.Contains("scale=1280:720", filter);
        Assert.Contains("force_original_aspect_ratio=decrease", filter);
        Assert.Contains("force_divisible_by=2", filter);
    }

    [Fact]
    public void Mp4IncludesVideoAndAudioEncoders()
    {
        VideoConversionOptions options = new("mp4", VideoScaleMode.Original, 100, 1920, 1080, VideoConversionQuality.Balanced);

        IReadOnlyList<string> arguments = VideoFfmpegArguments.Create(options);

        Assert.Contains("libopenh264", arguments);
        Assert.Contains("aac", arguments);
        Assert.Contains("+faststart", arguments);
    }
}
