namespace Glance.QuickConvert.Tests;

public sealed class YtDlpArgumentsTests
{
    [Fact]
    public void CreatesVideoArguments()
    {
        YtDlpConversionOptions options = new("mp4", 1080, @"C:\Downloads");

        IReadOnlyList<string> arguments = YtDlpArguments.Create(options,
            @"C:\ffmpeg",
            @"C:\deno.exe",
            "https://youtu.be/video");

        Assert.Contains("--recode-video", arguments);
        Assert.Contains("bv*[height<=1080]+ba/b[height<=1080]", arguments);
        Assert.DoesNotContain("--extract-audio", arguments);
        Assert.Equal("https://youtu.be/video", arguments[^1]);
    }

    [Fact]
    public void CreatesAudioArguments()
    {
        YtDlpConversionOptions options = new("mp3", 0, @"C:\Downloads");

        IReadOnlyList<string> arguments = YtDlpArguments.Create(options,
            @"C:\ffmpeg",
            @"C:\deno.exe",
            "https://youtu.be/video");

        Assert.Contains("--extract-audio", arguments);
        Assert.Contains("--audio-format", arguments);
        Assert.Contains("mp3", arguments);
        Assert.DoesNotContain("--recode-video", arguments);
    }

    [Fact]
    public void RejectsUnsupportedOutputFormat()
    {
        YtDlpConversionOptions options = new("pdf", 0, @"C:\Downloads");

        _ = Assert.Throws<ArgumentException>(() => YtDlpArguments.Create(options,
            @"C:\ffmpeg",
            @"C:\deno.exe",
            "https://youtu.be/video"));
    }
}
