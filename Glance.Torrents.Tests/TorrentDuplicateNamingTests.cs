namespace Glance.Torrents.Tests;

public sealed class TorrentDuplicateNamingTests
{
    [Fact]
    public void UsesOriginalNameWhenAvailable()
    {
        string result = TorrentDuplicateNaming.GetAvailableName("Example",
            _ => false);

        Assert.Equal("Example", result);
    }

    [Fact]
    public void UsesNextExplorerStyleSuffix()
    {
        HashSet<string> unavailable = ["Example", "Example (1)"];

        string result = TorrentDuplicateNaming.GetAvailableName("Example",
            unavailable.Contains);

        Assert.Equal("Example (2)", result);
    }
}
