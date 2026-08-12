using Glance.Application.Abstractions;

namespace Glance.Torrents.Tests;

public sealed class TorrentInputTests
{
    [Fact]
    public void AcceptsTorrentFile()
    {
        GlanceContentContext context = new(GlanceContentKind.FilesAndFolders, [new GlanceStorageItem(@"C:\Temp\sample.torrent", "sample.torrent", false)]);
        Assert.True(TorrentInput.TryCreate(context, out TorrentInput? input));
        Assert.Equal(TorrentInputKind.TorrentFile, input!.Kind);
    }

    [Theory]
    [InlineData(@"C:\Temp\sample.txt")]
    [InlineData(@"C:\Temp\torrent")]
    public void RejectsUnrelatedFiles(string path)
    {
        GlanceContentContext context = new(GlanceContentKind.FilesAndFolders, [new GlanceStorageItem(path, Path.GetFileName(path), false)]);
        Assert.False(TorrentInput.TryCreate(context, out _));
    }

    [Theory]
    [InlineData("magnet:?xt=urn:btih:0123456789abcdef0123456789abcdef01234567")]
    [InlineData("magnet:?xt=urn:btih:ABCDEFGHIJKLMNOPQRSTUVWXYZ234567")]
    [InlineData("magnet:?xt=urn:btmh:12200123456789abcdef")]
    public void AcceptsValidMagnetLinks(string value) => Assert.True(TorrentInputValidator.IsValidMagnet(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/file.torrent")]
    [InlineData("magnet:?dn=missing-hash")]
    [InlineData("magnet:?xt=urn:btih:short")]
    public void RejectsInvalidMagnetLinks(string? value) => Assert.False(TorrentInputValidator.IsValidMagnet(value));
}
