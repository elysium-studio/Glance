using System.Text.Json;

namespace Glance.Torrents.Tests;

public sealed class TorrentPersistenceTests
{
    [Fact]
    public void StateRoundTripsForRestartRestoration()
    {
        TorrentStateDocument original = new([new TorrentPersistedDownload("id", new TorrentInput(TorrentInputKind.TorrentFile, "a.torrent"), "downloads", ["a.bin"], true, true)]);
        string json = JsonSerializer.Serialize(original, TorrentJsonContext.Default.TorrentStateDocument);
        TorrentStateDocument restored = JsonSerializer.Deserialize(json, TorrentJsonContext.Default.TorrentStateDocument)!;
        Assert.Equal(original.Downloads[0].Id, restored.Downloads[0].Id);
        Assert.Equal(original.Downloads[0].Input, restored.Downloads[0].Input);
        Assert.Equal(original.Downloads[0].DownloadPath, restored.Downloads[0].DownloadPath);
        Assert.Equal(original.Downloads[0].SelectedFiles, restored.Downloads[0].SelectedFiles);
        Assert.Equal(original.Downloads[0].WasPaused, restored.Downloads[0].WasPaused);
        Assert.True(restored.Downloads[0].CompletionNotified);
    }
}
