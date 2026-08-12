namespace Glance.Torrents.Tests;

public sealed class TorrentPolicyTests
{
    [Fact]
    public void NormalizeClampsInvalidNumbers()
    {
        TorrentSettings result = TorrentSettings.Normalize(new TorrentSettings { MaximumDownloadKilobytesPerSecond = -1, MaximumUploadKilobytesPerSecond = -2, MaximumPeersPerTorrent = -3, SeedingRatioLimit = -1, SeedingTimeLimitMinutes = -2 });
        Assert.Equal(0, result.MaximumDownloadKilobytesPerSecond);
        Assert.Equal(0, result.MaximumUploadKilobytesPerSecond);
        Assert.Equal(0, result.MaximumPeersPerTorrent);
        Assert.Equal(0, result.SeedingRatioLimit);
        Assert.Equal(0, result.SeedingTimeLimitMinutes);
    }

    [Fact]
    public void SeedingCanStopImmediately() => Assert.True(TorrentSeedingPolicy.ShouldStop(new TorrentSettings { ContinueSeeding = false }, 1, 0, TimeSpan.Zero));

    [Theory]
    [InlineData(TorrentSeedingLimitMode.Either, 200, 100, 5, true)]
    [InlineData(TorrentSeedingLimitMode.Both, 200, 100, 5, false)]
    [InlineData(TorrentSeedingLimitMode.Both, 200, 100, 15, true)]
    public void SeedingLimitsRespectEitherOrBoth(TorrentSeedingLimitMode mode, long downloaded, long uploaded, int minutes, bool expected)
    {
        TorrentSettings settings = new() { ContinueSeeding = true, SeedingRatioLimit = .5, SeedingTimeLimitMinutes = 10, SeedingLimitMode = mode };
        Assert.Equal(expected, TorrentSeedingPolicy.ShouldStop(settings, downloaded, uploaded, TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void CompletionAttentionIsDeduplicated()
    {
        TorrentCompletionTracker tracker = new(["b"]);
        Assert.True(tracker.TryMarkCompleted("a"));
        Assert.False(tracker.TryMarkCompleted("a"));
        Assert.False(tracker.TryMarkCompleted("b"));
    }

    [Fact]
    public void ViewModelMapsStateAndAggregatesRawRates()
    {
        TorrentsViewModel viewModel = new();
        viewModel.Update(Snapshot("a", TorrentDownloadState.Downloading, 1536));
        viewModel.Update(Snapshot("b", TorrentDownloadState.Downloading, 1536));
        Assert.Equal(2, viewModel.ActiveCount);
        Assert.Equal("3 KB/s", viewModel.TotalDownloadSpeedText);
        Assert.True(viewModel.Torrents[0].CanPause);
    }

    private static TorrentTransferSnapshot Snapshot(string id, TorrentDownloadState state, long speed) => new(id, id, state, 50, speed, 0, 1, 5, 0, 10, TimeSpan.Zero);
}
