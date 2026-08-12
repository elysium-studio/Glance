namespace Glance.Torrents.Tests;

public sealed class TorrentCoordinatorTests
{
    [Fact]
    public async Task ConfirmationStartsSelectedFiles()
    {
        FakeEngine engine = new();
        await using TorrentAddCoordinator coordinator = new(engine);
        TorrentMetadataSession session = await coordinator.PrepareAsync(Input(), "downloads", TimeSpan.FromSeconds(1));
        await coordinator.ConfirmAsync(session, ["one.txt"]);
        Assert.Equal(session.SessionId, engine.ConfirmedSession);
        Assert.Equal(["one.txt"], engine.SelectedFiles);
    }

    [Fact]
    public async Task CancellationCleansPendingMetadata()
    {
        FakeEngine engine = new();
        await using TorrentAddCoordinator coordinator = new(engine);
        TorrentMetadataSession session = await coordinator.PrepareAsync(Input(), "downloads", TimeSpan.FromSeconds(1));
        await coordinator.CancelAsync(session);
        Assert.Contains(session.SessionId, engine.CancelledSessions);
    }

    [Fact]
    public async Task DuplicateTorrentIsRejectedAndCleaned()
    {
        FakeEngine engine = new();
        await using TorrentAddCoordinator coordinator = new(engine);
        _ = await coordinator.PrepareAsync(Input(), "downloads", TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.PrepareAsync(Input(), "downloads", TimeSpan.FromSeconds(1)));
        Assert.Single(engine.CancelledSessions);
    }

    [Fact]
    public async Task PauseResumeAndRuntimeLimitsReachEngine()
    {
        FakeEngine engine = new();
        await engine.PauseAsync("a");
        await engine.ResumeAsync("a");
        await engine.ApplySettingsAsync(new TorrentSettings { MaximumDownloadKilobytesPerSecond = 10 });
        Assert.True(engine.Paused && engine.Resumed);
        Assert.Equal(10, engine.AppliedSettings!.MaximumDownloadKilobytesPerSecond);
    }

    [Fact]
    public async Task DisposalCancelsEveryPendingSessionAndIsIdempotent()
    {
        FakeEngine engine = new();
        TorrentAddCoordinator coordinator = new(engine);
        TorrentMetadataSession session = await coordinator.PrepareAsync(Input(), "downloads", TimeSpan.FromSeconds(1));
        await coordinator.DisposeAsync();
        await coordinator.DisposeAsync();
        Assert.Equal([session.SessionId], engine.CancelledSessions);
    }

    [Fact]
    public async Task MetadataCancellationAndTimeoutPropagate()
    {
        FakeEngine engine = new() { Resolve = async token => { await Task.Delay(Timeout.Infinite, token); return null!; } };
        await using TorrentAddCoordinator coordinator = new(engine);
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.PrepareAsync(Input(), "downloads", TimeSpan.FromSeconds(1), cancellation.Token));
    }

    private static TorrentInput Input() => new(TorrentInputKind.MagnetLink, "magnet:?xt=urn:btih:0123456789abcdef0123456789abcdef01234567");

    private sealed class FakeEngine : ITorrentEngineService
    {
        private int sequence;
        public Func<CancellationToken, Task<TorrentMetadataSession>>? Resolve { get; init; }
        public event EventHandler<TorrentSnapshotEventArgs>? SnapshotUpdated;
        public event EventHandler<TorrentCompletedEventArgs>? TorrentCompleted;
        public IReadOnlyCollection<string> ActiveTorrentIds { get; init; } = [];
        public string? ConfirmedSession { get; private set; }
        public IReadOnlyCollection<string>? SelectedFiles { get; private set; }
        public List<string> CancelledSessions { get; } = [];
        public bool Paused { get; private set; }
        public bool Resumed { get; private set; }
        public TorrentSettings? AppliedSettings { get; private set; }
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TorrentMetadataSession> ResolveMetadataAsync(TorrentInput input, string downloadPath, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (Resolve is not null) return Resolve(cancellationToken);
            return Task.FromResult(new TorrentMetadataSession($"s{Interlocked.Increment(ref sequence)}", "same-hash", input, "sample", 1, [new TorrentMetadataFile("one.txt", 1)], [], downloadPath));
        }
        public Task ConfirmAsync(string sessionId, IReadOnlyCollection<string> selectedFiles, CancellationToken cancellationToken = default) { ConfirmedSession = sessionId; SelectedFiles = selectedFiles; return Task.CompletedTask; }
        public Task CancelMetadataAsync(string sessionId) { CancelledSessions.Add(sessionId); return Task.CompletedTask; }
        public Task PauseAsync(string torrentId) { Paused = true; return Task.CompletedTask; }
        public Task ResumeAsync(string torrentId) { Resumed = true; return Task.CompletedTask; }
        public Task RemoveAsync(string torrentId, bool deleteData) => Task.CompletedTask;
        public Task ApplySettingsAsync(TorrentSettings settings, CancellationToken cancellationToken = default) { AppliedSettings = settings; return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
