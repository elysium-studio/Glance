using Glance.Application.Abstractions;
using MonoTorrent;
using MonoTorrent.Client;
using System.Collections.Concurrent;
using System.Text.Json;
using CoreSettings = Glance.Torrents.TorrentSettings;

namespace Glance.Torrents.WinUI;

public sealed class MonoTorrentEngineService : ITorrentEngineService
{
    private sealed record PendingMetadata(TorrentMetadataSession Session, byte[] Metadata);
    private sealed class ManagedDownload(TorrentManager manager, TorrentPersistedDownload persisted)
    {
        public TorrentManager Manager { get; } = manager;
        public TorrentPersistedDownload Persisted { get; set; } = persisted;
        public DateTimeOffset? SeedingStarted { get; set; }
    }

    private readonly GlanceModuleOptions<CoreSettings> options;
    private readonly ConcurrentDictionary<string, PendingMetadata> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ManagedDownload> downloads = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private readonly CancellationTokenSource disposalCancellation = new();
    private readonly string rootPath;
    private readonly string cachePath;
    private readonly string metadataPath;
    private readonly string statePath;
    private ClientEngine? engine;
    private PeriodicTimer? snapshotTimer;
    private Task? snapshotWorker;
    private int disposed;

    public MonoTorrentEngineService(GlanceModuleOptions<CoreSettings> options)
    {
        this.options = options;
        rootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance", "Modules", "Torrent", "Data");
        cachePath = Path.Combine(rootPath, "Engine");
        metadataPath = Path.Combine(rootPath, "Metadata");
        statePath = Path.Combine(rootPath, "downloads.json");
    }

    public event EventHandler<TorrentSnapshotEventArgs>? SnapshotUpdated;
    public event EventHandler<TorrentCompletedEventArgs>? TorrentCompleted;

    public IReadOnlyCollection<string> ActiveTorrentIds => downloads.Keys.ToArray();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (engine is not null) return;
            Directory.CreateDirectory(cachePath);
            Directory.CreateDirectory(metadataPath);
            CoreSettings settings = CoreSettings.Normalize(options.Current);
            engine = new ClientEngine(CreateEngineSettings(settings));
            await RestoreAsync(settings, cancellationToken);
            snapshotTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            snapshotWorker = RunSnapshotLoopAsync(snapshotTimer, disposalCancellation.Token);
        }
        finally
        {
            _ = lifecycle.Release();
        }
    }

    public async Task<TorrentMetadataSession> ResolveMetadataAsync(TorrentInput input, string downloadPath, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ClientEngine current = GetEngine();
        using CancellationTokenSource timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disposalCancellation.Token);
        timeoutCancellation.CancelAfter(timeout);
        try
        {
            byte[] bytes;
            Torrent torrent;
            if (input.Kind == TorrentInputKind.TorrentFile)
            {
                if (!TorrentInputValidator.IsValidTorrentPath(input.Value)) throw new ArgumentException("A valid .torrent file is required.", nameof(input));
                bytes = await File.ReadAllBytesAsync(input.Value, timeoutCancellation.Token);
                torrent = await Torrent.LoadAsync(bytes);
            }
            else
            {
                MagnetLink magnet = MagnetLink.Parse(input.Value);
                bytes = (await current.DownloadMetadataAsync(magnet, timeoutCancellation.Token)).ToArray();
                torrent = await Torrent.LoadAsync(bytes);
            }

            timeoutCancellation.Token.ThrowIfCancellationRequested();
            string id = torrent.InfoHashes.V1OrV2.ToHex();
            string sessionId = Guid.NewGuid().ToString("N");
            TorrentMetadataSession session = new(sessionId, id, input, torrent.Name, torrent.Size,
                torrent.Files.Select(file => new TorrentMetadataFile(file.Path, file.Length)).ToArray(),
                torrent.AnnounceUrls.SelectMany(tier => tier).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                downloadPath);
            if (!pending.TryAdd(sessionId, new PendingMetadata(session, bytes))) throw new InvalidOperationException("Could not create the torrent confirmation session.");
            return session;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !disposalCancellation.IsCancellationRequested)
        {
            throw new TimeoutException("Torrent metadata retrieval timed out.");
        }
    }

    public async Task ConfirmAsync(string sessionId, IReadOnlyCollection<string> selectedFiles, CancellationToken cancellationToken = default)
    {
        if (!pending.TryRemove(sessionId, out PendingMetadata? value)) throw new InvalidOperationException("The torrent confirmation has expired.");
        if (downloads.ContainsKey(value.Session.TorrentId)) throw new InvalidOperationException("This torrent is already in Glance.");
        Directory.CreateDirectory(value.Session.DownloadPath);
        string cachedMetadata = Path.Combine(metadataPath, $"{value.Session.TorrentId}.torrent");
        await File.WriteAllBytesAsync(cachedMetadata, value.Metadata, cancellationToken);
        TorrentManager manager = await GetEngine().AddAsync(cachedMetadata, value.Session.DownloadPath, CreateTorrentSettings(options.Current));
        HashSet<string> selection = new(selectedFiles, StringComparer.OrdinalIgnoreCase);
        foreach (var file in manager.Files)
        {
            await manager.SetFilePriorityAsync(file, selection.Contains(file.Path) ? Priority.Normal : Priority.DoNotDownload);
        }
        TorrentPersistedDownload persisted = new(value.Session.TorrentId, value.Session.Input, value.Session.DownloadPath, selection.ToArray(), false, false);
        ManagedDownload managed = new(manager, persisted);
        if (!downloads.TryAdd(persisted.Id, managed))
        {
            _ = await GetEngine().RemoveAsync(manager, RemoveMode.CacheDataOnly);
            throw new InvalidOperationException("This torrent is already in Glance.");
        }
        manager.TorrentStateChanged += HandleTorrentStateChanged;
        await SaveStateAsync(cancellationToken);
        await manager.StartAsync();
        Publish(managed);
    }

    public Task CancelMetadataAsync(string sessionId)
    {
        _ = pending.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    public async Task PauseAsync(string torrentId)
    {
        ManagedDownload managed = GetDownload(torrentId);
        await managed.Manager.PauseAsync();
        managed.Persisted = managed.Persisted with { WasPaused = true };
        await SaveStateAsync();
        Publish(managed);
    }

    public async Task ResumeAsync(string torrentId)
    {
        ManagedDownload managed = GetDownload(torrentId);
        managed.Persisted = managed.Persisted with { WasPaused = false };
        await managed.Manager.StartAsync();
        await SaveStateAsync();
        Publish(managed);
    }

    public async Task RemoveAsync(string torrentId, bool deleteData)
    {
        if (!downloads.TryRemove(torrentId, out ManagedDownload? managed)) return;
        managed.Manager.TorrentStateChanged -= HandleTorrentStateChanged;
        await managed.Manager.StopAsync(TimeSpan.FromSeconds(2));
        _ = await GetEngine().RemoveAsync(managed.Manager, deleteData ? RemoveMode.CacheDataAndDownloadedData : RemoveMode.CacheDataOnly);
        await SaveStateAsync();
    }

    public async Task ApplySettingsAsync(CoreSettings settings, CancellationToken cancellationToken = default)
    {
        settings = CoreSettings.Normalize(settings);
        ClientEngine current = GetEngine();
        await current.UpdateSettingsAsync(CreateEngineSettings(settings));
        foreach (ManagedDownload managed in downloads.Values)
        {
            await managed.Manager.UpdateSettingsAsync(CreateTorrentSettings(settings));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        disposalCancellation.Cancel();
        snapshotTimer?.Dispose();
        if (snapshotWorker is not null)
        {
            try { await snapshotWorker; } catch (OperationCanceledException) { }
        }
        await lifecycle.WaitAsync();
        try
        {
            pending.Clear();
            if (engine is not null)
            {
                await SaveStateAsync();
                await engine.StopAllAsync(TimeSpan.FromSeconds(2));
                foreach (ManagedDownload managed in downloads.Values) managed.Manager.TorrentStateChanged -= HandleTorrentStateChanged;
                downloads.Clear();
                engine.Dispose();
                engine = null;
            }
        }
        finally
        {
            _ = lifecycle.Release();
            lifecycle.Dispose();
            disposalCancellation.Dispose();
        }
    }

    private async Task RestoreAsync(CoreSettings settings, CancellationToken cancellationToken)
    {
        if (!File.Exists(statePath)) return;
        TorrentStateDocument? state;
        await using (FileStream stream = File.OpenRead(statePath))
        {
            state = await JsonSerializer.DeserializeAsync(stream, TorrentJsonContext.Default.TorrentStateDocument, cancellationToken);
        }
        if (state is null) return;
        foreach (TorrentPersistedDownload persisted in state.Downloads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                string cachedMetadata = Path.Combine(metadataPath, $"{persisted.Id}.torrent");
                TorrentManager manager = File.Exists(cachedMetadata)
                    ? await GetEngine().AddAsync(cachedMetadata, persisted.DownloadPath, CreateTorrentSettings(settings))
                    : await GetEngine().AddAsync(MagnetLink.Parse(persisted.Input.Value), persisted.DownloadPath, CreateTorrentSettings(settings));
                HashSet<string> selection = new(persisted.SelectedFiles, StringComparer.OrdinalIgnoreCase);
                if (manager.HasMetadata)
                {
                    foreach (var file in manager.Files) await manager.SetFilePriorityAsync(file, selection.Contains(file.Path) ? Priority.Normal : Priority.DoNotDownload);
                }
                ManagedDownload managed = new(manager, persisted);
                downloads[persisted.Id] = managed;
                manager.TorrentStateChanged += HandleTorrentStateChanged;
                if (persisted.WasPaused) await manager.PauseAsync(); else await manager.StartAsync();
                Publish(managed);
            }
            catch
            {
                // One damaged entry must not prevent the remaining downloads from being restored.
            }
        }
    }

    private async Task RunSnapshotLoopAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            foreach (ManagedDownload managed in downloads.Values)
            {
                Publish(managed);
                await EnforceSeedingLimitsAsync(managed);
            }
        }
    }

    private async Task EnforceSeedingLimitsAsync(ManagedDownload managed)
    {
        if (managed.Manager.State != TorrentState.Seeding) return;
        managed.SeedingStarted ??= DateTimeOffset.UtcNow;
        CoreSettings settings = CoreSettings.Normalize(options.Current);
        long selectedSize = managed.Manager.Files.Where(file => file.Priority != Priority.DoNotDownload).Sum(file => file.Length);
        TimeSpan time = DateTimeOffset.UtcNow - managed.SeedingStarted.Value;
        if (TorrentSeedingPolicy.ShouldStop(settings, selectedSize, managed.Manager.Monitor.DataBytesSent, time))
        {
            await managed.Manager.StopAsync(TimeSpan.FromSeconds(2));
            Publish(managed);
        }
    }

    private void HandleTorrentStateChanged(object? sender, TorrentStateChangedEventArgs args)
    {
        if (sender is not TorrentManager manager) return;
        ManagedDownload? managed = downloads.Values.FirstOrDefault(value => ReferenceEquals(value.Manager, manager));
        if (managed is null) return;
        Publish(managed);
    }

    private void Publish(ManagedDownload managed)
    {
        TorrentManager manager = managed.Manager;
        TorrentDownloadState state = MapState(manager.State, manager.Error);
        long selectedSize = manager.HasMetadata ? manager.Files.Where(file => file.Priority != Priority.DoNotDownload).Sum(file => file.Length) : 0;
        TorrentTransferSnapshot snapshot = new(managed.Persisted.Id,
            manager.HasMetadata ? manager.Torrent?.Name ?? "Torrent download" : manager.MagnetLink?.Name ?? "Magnet download",
            state,
            manager.HasMetadata ? manager.PartialProgress : 0,
            manager.Monitor.DownloadRate,
            manager.Monitor.UploadRate,
            manager.OpenConnections,
            (long)(selectedSize * Math.Clamp(manager.PartialProgress, 0, 100) / 100d),
            manager.Monitor.DataBytesSent,
            selectedSize,
            managed.SeedingStarted is null ? TimeSpan.Zero : DateTimeOffset.UtcNow - managed.SeedingStarted.Value,
            manager.Error?.Exception?.Message);
        SnapshotUpdated?.Invoke(this, new TorrentSnapshotEventArgs(snapshot));
        if (state is TorrentDownloadState.Seeding or TorrentDownloadState.Completed && !managed.Persisted.CompletionNotified)
        {
            managed.Persisted = managed.Persisted with { CompletionNotified = true };
            TorrentCompleted?.Invoke(this, new TorrentCompletedEventArgs(managed.Persisted.Id));
            _ = SaveStateAsync();
        }
    }

    private static TorrentDownloadState MapState(TorrentState state, Error? error) => error is not null
        ? TorrentDownloadState.Error
        : state switch
        {
            TorrentState.Metadata => TorrentDownloadState.RetrievingMetadata,
            TorrentState.Hashing or TorrentState.HashingPaused => TorrentDownloadState.Checking,
            TorrentState.Downloading => TorrentDownloadState.Downloading,
            TorrentState.Paused => TorrentDownloadState.Paused,
            TorrentState.Seeding => TorrentDownloadState.Seeding,
            TorrentState.Stopped => TorrentDownloadState.Stopped,
            _ => TorrentDownloadState.Queued
        };

    private EngineSettings CreateEngineSettings(CoreSettings settings) => new EngineSettingsBuilder
    {
        CacheDirectory = cachePath,
        AutoSaveLoadDhtCache = true,
        AutoSaveLoadFastResume = true,
        AutoSaveLoadMagnetLinkMetadata = true,
        MaximumDownloadRate = ToBytesPerSecond(settings.MaximumDownloadKilobytesPerSecond),
        MaximumUploadRate = ToBytesPerSecond(settings.MaximumUploadKilobytesPerSecond)
    }.ToSettings();

    private static MonoTorrent.Client.TorrentSettings CreateTorrentSettings(CoreSettings settings) => new TorrentSettingsBuilder
    {
        MaximumConnections = settings.MaximumPeersPerTorrent == 0 ? 60 : settings.MaximumPeersPerTorrent,
        CreateContainingDirectory = true
    }.ToSettings();

    private async Task SaveStateAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(rootPath);
        string temporary = statePath + ".tmp";
        TorrentStateDocument state = new(downloads.Values.Select(value => value.Persisted).ToArray());
        await using (FileStream stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(stream, state, TorrentJsonContext.Default.TorrentStateDocument, cancellationToken);
        }
        File.Move(temporary, statePath, true);
    }

    private ClientEngine GetEngine() => engine ?? throw new InvalidOperationException("The torrent engine has not been initialized.");
    private ManagedDownload GetDownload(string id) => downloads.TryGetValue(id, out ManagedDownload? value) ? value : throw new KeyNotFoundException("Torrent not found.");
    private static int ToBytesPerSecond(int kilobytes) => kilobytes <= 0 ? 0 : (int)Math.Min(int.MaxValue, kilobytes * 1024L);
}
