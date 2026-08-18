namespace Glance.QuickConvert.Tests;

public sealed class YtDlpUrlMatcherTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=2s3iGpDqQpQ&list=RD2s3iGpDqQpQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://soundcloud.com/artist/track")]
    [InlineData("https://example.com/media/video.mp4")]
    [InlineData("https://example.com/article-with-embedded-video")]
    public void RecognizesSupportedMediaUrls(string value) => Assert.True(YtDlpUrlMatcher.IsSupported(value));

    [Theory]
    [InlineData("https://open.spotify.com/track/123")]
    [InlineData("not a url")]
    [InlineData("")]
    public void RejectsUnrelatedUrls(string value) => Assert.False(YtDlpUrlMatcher.IsSupported(value));
}
