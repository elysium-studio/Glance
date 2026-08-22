using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using MonoTorrent;
using MonoTorrent.Client;
using System.Collections.Concurrent;
using System.Text.Json;
using CoreSettings = Glance.Torrents.TorrentSettings;

namespace Glance.Torrents.WinUI;

public sealed class MonoTorrentEngineService : ITorrentEngineService
{
    private sealed record PendingMetadata(TorrentMetadataSession Session, byte[] Metadata);
    private sealed record DownloadTarget(string Id,
        string DisplayName,
        string SavePath,
        bool CreateContainingDirectory);
    private sealed class ManagedDownload(ClientEngine client,
        TorrentManager manager,
        TorrentPersistedDownload persisted)
    {
        public ClientEngine Client { get; } = client;
        public TorrentManager Manager { get; } = manager;
        public TorrentPersistedDownload Persisted { get; set; } = persisted;
        public DateTimeOffset? SeedingStarted { get; set; }
    }

    private readonly GlanceModuleOptions<CoreSettings> options;
    private readonly ConcurrentDictionary<string, PendingMetadata> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ManagedDownload> downloads = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<ClientEngine, string> clients = new();
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
        rootPath = Path.Combine(GlanceModuleData.GetDirectory("Torrent"), "Data");
        cachePath = Path.Combine(rootPath, "Engine");
        metadataPath = Path.Combine(rootPath, "Metadata");
        statePath = Path.Combine(rootPath, "downloads.json");
    }

    public event EventHandler<TorrentSnapshotEventArgs>? SnapshotUpdated;
    public event EventHandler<TorrentCompletedEventArgs>? TorrentCompleted;

    public IReadOnlyCollection<string> ActiveTorrentIds => [.. downloads.Keys];

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
            engine = CreateClient(settings,
                cachePath);
            await RestoreAsync(settings, cancellationToken);
            snapshotTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            snapshotWorker = RunSnapshotLoopAsync(snapshotTimer, disposalCancellation.Token);
        }
        finally
        {
            _ = lifecycle.Release();
        }
    }

    public async Task<TorrentMetadataSession> ResolveMetadataAsync(TorrentInput input,
        string downloadPath,
        TimeSpan magnetMetadataTimeout,
        CancellationToken cancellationToken = default)
    {
        ClientEngine current = GetEngine();
        using CancellationTokenSource operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
            disposalCancellation.Token);

        if (input.Kind == TorrentInputKind.MagnetLink)
        {
            operationCancellation.CancelAfter(magnetMetadataTimeout);
        }

        try
        {
            byte[] bytes;

            if (input.Kind == TorrentInputKind.TorrentFile)
            {
                if (!TorrentInputValidator.IsValidTorrentPath(input.Value)) throw new ArgumentException("A valid .torrent file is required.", nameof(input));
                bytes = await File.ReadAllBytesAsync(input.Value, operationCancellation.Token);
            }
            else
            {
                MagnetLink magnet = MagnetLink.Parse(input.Value);
                bytes = (await current.DownloadMetadataAsync(magnet, operationCancellation.Token)).ToArray();
            }

            operationCancellation.Token.ThrowIfCancellationRequested();
            Torrent torrent = await Task.Run(() => Torrent.Load(bytes),
                operationCancellation.Token).WaitAsync(operationCancellation.Token);
            string id = torrent.InfoHashes.V1OrV2.ToHex();
            string sessionId = Guid.NewGuid().ToString("N");
            TorrentMetadataSession session = new(sessionId, id, input, torrent.Name, torrent.Size,
                [.. torrent.Files.Select(file => new TorrentMetadataFile(file.Path, file.Length))],
                [.. torrent.AnnounceUrls.SelectMany(tier => tier).Distinct(StringComparer.OrdinalIgnoreCase)],
                downloadPath);
            if (!pending.TryAdd(sessionId, new PendingMetadata(session, bytes))) throw new InvalidOperationException("Could not create the torrent confirmation session.");
            return session;
        }
        catch (OperationCanceledException) when (input.Kind == TorrentInputKind.MagnetLink &&
            !cancellationToken.IsCancellationRequested &&
            !disposalCancellation.IsCancellationRequested)
        {
            throw new TimeoutException("Magnet metadata retrieval timed out.");
        }
    }

    public async Task ConfirmAsync(string sessionId,
        IReadOnlyCollection<string> selectedFiles,
        string downloadPath,
        CancellationToken cancellationToken = default)
    {
        if (!pending.TryRemove(sessionId, out PendingMetadata? value)) throw new InvalidOperationException("The torrent confirmation has expired.");
        Torrent torrent = Torrent.Load(value.Metadata);
        DownloadTarget target = ResolveDownloadTarget(value.Session.TorrentId,
            torrent.Name,
            downloadPath);
        Directory.CreateDirectory(target.SavePath);
        string cachedMetadata = Path.Combine(metadataPath, $"{target.Id}.torrent");
        await File.WriteAllBytesAsync(cachedMetadata, value.Metadata, cancellationToken);
        ClientEngine client = GetAvailableClient(torrent.InfoHashes,
            options.Current,
            target.Id);
        await RebalanceClientLimitsAsync(options.Current);
        TorrentManager manager = await client.AddAsync(cachedMetadata,
            target.SavePath,
            CreateTorrentSettings(options.Current,
                target.CreateContainingDirectory));
        HashSet<string> selection = new(selectedFiles, StringComparer.OrdinalIgnoreCase);
        foreach (ITorrentManagerFile file in manager.Files)
        {
            await manager.SetFilePriorityAsync(file, selection.Contains(file.Path) ? Priority.Normal : Priority.DoNotDownload);
        }
        TorrentPersistedDownload persisted = new(target.Id,
            value.Session.Input,
            target.SavePath,
            [.. selection],
            false,
            false,
            value.Session.TorrentId,
            target.DisplayName,
            target.CreateContainingDirectory);
        ManagedDownload managed = new(client,
            manager,
            persisted);
        if (!downloads.TryAdd(persisted.Id, managed))
        {
            _ = await client.RemoveAsync(manager, RemoveMode.CacheDataOnly);
            throw new InvalidOperationException("Could not register the torrent download.");
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
        string? ownedDownloadDirectory = deleteData
            ? GetOwnedDownloadDirectory(managed)
            : null;
        managed.Manager.TorrentStateChanged -= HandleTorrentStateChanged;
        await managed.Manager.StopAsync(TimeSpan.FromSeconds(2));
        _ = await managed.Client.RemoveAsync(managed.Manager,
            deleteData ? RemoveMode.CacheDataAndDownloadedData : RemoveMode.CacheDataOnly);
        DeleteEmptyDirectoryTree(ownedDownloadDirectory);

        if (!ReferenceEquals(managed.Client, engine) && managed.Client.Torrents.Count == 0 && clients.TryRemove(managed.Client, out _))
        {
            managed.Client.Dispose();
            await RebalanceClientLimitsAsync(options.Current);
        }

        await SaveStateAsync();
    }

    public async Task ApplySettingsAsync(CoreSettings settings, CancellationToken cancellationToken = default)
    {
        settings = CoreSettings.Normalize(settings);
        await RebalanceClientLimitsAsync(settings);
        foreach (ManagedDownload managed in downloads.Values)
        {
            await managed.Manager.UpdateSettingsAsync(CreateTorrentSettings(settings,
                managed.Persisted.CreateContainingDirectory));
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
                foreach (ManagedDownload managed in downloads.Values) managed.Manager.TorrentStateChanged -= HandleTorrentStateChanged;
                foreach (ClientEngine client in clients.Keys)
                {
                    await client.StopAllAsync(TimeSpan.FromSeconds(2));
                }
                downloads.Clear();
                foreach (ClientEngine client in clients.Keys)
                {
                    client.Dispose();
                }
                clients.Clear();
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
                Torrent? torrent = File.Exists(cachedMetadata)
                    ? Torrent.Load(cachedMetadata)
                    : null;
                MagnetLink? magnet = torrent is null
                    ? MagnetLink.Parse(persisted.Input.Value)
                    : null;
                InfoHashes infoHashes = torrent?.InfoHashes ?? magnet!.InfoHashes;
                ClientEngine client = GetAvailableClient(infoHashes,
                    settings,
                    persisted.Id);
                TorrentManager manager = File.Exists(cachedMetadata)
                    ? await client.AddAsync(cachedMetadata,
                        persisted.DownloadPath,
                        CreateTorrentSettings(settings,
                            persisted.CreateContainingDirectory))
                    : await client.AddAsync(magnet!,
                        persisted.DownloadPath,
                        CreateTorrentSettings(settings,
                            persisted.CreateContainingDirectory));
                HashSet<string> selection = new(persisted.SelectedFiles, StringComparer.OrdinalIgnoreCase);
                if (manager.HasMetadata)
                {
                    foreach (ITorrentManagerFile file in manager.Files) await manager.SetFilePriorityAsync(file, selection.Contains(file.Path) ? Priority.Normal : Priority.DoNotDownload);
                }
                TorrentPersistedDownload restored = persisted with
                {
                    InfoHash = persisted.InfoHash ?? infoHashes.V1OrV2.ToHex(),
                    DisplayName = persisted.DisplayName ?? torrent?.Name ?? magnet?.Name ?? "Torrent download"
                };
                ManagedDownload managed = new(client,
                    manager,
                    restored);
                downloads[restored.Id] = managed;
                manager.TorrentStateChanged += HandleTorrentStateChanged;
                if (persisted.WasPaused) await manager.PauseAsync(); else await manager.StartAsync();
                Publish(managed);
            }
            catch
            {
                // One damaged entry must not prevent the remaining downloads from being restored.
            }
        }

        await RebalanceClientLimitsAsync(settings);
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
            managed.Persisted.DisplayName ?? (manager.HasMetadata
                ? manager.Torrent?.Name ?? "Torrent download"
                : manager.MagnetLink?.Name ?? "Magnet download"),
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

    private ClientEngine CreateClient(CoreSettings settings,
        string clientCachePath)
    {
        Directory.CreateDirectory(clientCachePath);
        ClientEngine client = new(CreateEngineSettings(settings,
            clientCachePath,
            clients.Count + 1));
        clients[client] = clientCachePath;
        return client;
    }

    private ClientEngine GetAvailableClient(InfoHashes infoHashes,
        CoreSettings settings,
        string instanceId)
    {
        ClientEngine? available = clients.Keys.FirstOrDefault(client => !client.Contains(infoHashes));
        return available ?? CreateClient(settings,
            Path.Combine(cachePath, instanceId));
    }

    private async Task RebalanceClientLimitsAsync(CoreSettings settings)
    {
        settings = CoreSettings.Normalize(settings);
        KeyValuePair<ClientEngine, string>[] currentClients = [.. clients];
        int clientCount = Math.Max(1, currentClients.Length);

        foreach ((ClientEngine client, string clientCachePath) in currentClients)
        {
            await client.UpdateSettingsAsync(CreateEngineSettings(settings,
                clientCachePath,
                clientCount));
        }
    }

    private DownloadTarget ResolveDownloadTarget(string infoHash,
        string torrentName,
        string downloadPath)
    {
        HashSet<string> activeNames = new(downloads.Values
            .Where(download => string.Equals(download.Persisted.InfoHash,
                infoHash,
                StringComparison.OrdinalIgnoreCase))
            .Select(download => download.Persisted.DisplayName ?? string.Empty),
            StringComparer.OrdinalIgnoreCase);
        string displayName = TorrentDuplicateNaming.GetAvailableName(torrentName,
            candidate => activeNames.Contains(candidate) ||
                Directory.Exists(Path.Combine(downloadPath, candidate)) ||
                File.Exists(Path.Combine(downloadPath, candidate)));
        bool isOriginalName = string.Equals(displayName,
            torrentName,
            StringComparison.Ordinal);

        return new DownloadTarget($"{infoHash}-{Guid.NewGuid():N}",
            displayName,
            isOriginalName ? downloadPath : Path.Combine(downloadPath, displayName),
            isOriginalName);
    }

    private static EngineSettings CreateEngineSettings(CoreSettings settings,
        string clientCachePath,
        int clientCount) => new EngineSettingsBuilder
    {
        CacheDirectory = clientCachePath,
        AutoSaveLoadDhtCache = true,
        AutoSaveLoadFastResume = true,
        AutoSaveLoadMagnetLinkMetadata = true,
        MaximumDownloadRate = DivideLimit(ToBytesPerSecond(settings.MaximumDownloadKilobytesPerSecond),
            clientCount),
        MaximumUploadRate = DivideLimit(ToBytesPerSecond(settings.MaximumUploadKilobytesPerSecond),
            clientCount)
    }.ToSettings();

    private static MonoTorrent.Client.TorrentSettings CreateTorrentSettings(CoreSettings settings,
        bool createContainingDirectory = true) => new TorrentSettingsBuilder
    {
        MaximumConnections = settings.MaximumPeersPerTorrent == 0 ? 60 : settings.MaximumPeersPerTorrent,
        CreateContainingDirectory = createContainingDirectory
    }.ToSettings();

    private async Task SaveStateAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(rootPath);
        string temporary = statePath + ".tmp";
        TorrentStateDocument state = new([.. downloads.Values.Select(value => value.Persisted)]);
        await using (FileStream stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(stream, state, TorrentJsonContext.Default.TorrentStateDocument, cancellationToken);
        }
        File.Move(temporary, statePath, true);
    }

    private static string? GetOwnedDownloadDirectory(ManagedDownload managed)
    {
        string savePath = Path.GetFullPath(managed.Manager.SavePath);
        string? containingDirectory = managed.Manager.ContainingDirectory;

        if (!managed.Persisted.CreateContainingDirectory)
        {
            string directoryName = Path.GetFileName(savePath.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
            return !string.IsNullOrWhiteSpace(managed.Persisted.DisplayName) &&
                string.Equals(directoryName,
                    managed.Persisted.DisplayName,
                    StringComparison.OrdinalIgnoreCase)
                ? savePath
                : null;
        }

        if (string.IsNullOrWhiteSpace(containingDirectory))
        {
            return null;
        }

        string candidate = Path.GetFullPath(containingDirectory);
        string relativePath = Path.GetRelativePath(savePath,
            candidate);
        return relativePath != "." &&
            relativePath != ".." &&
            !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal) &&
            !Path.IsPathRooted(relativePath)
            ? candidate
            : null;
    }

    private static void DeleteEmptyDirectoryTree(string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return;
        }

        string[] directories;

        try
        {
            directories = Directory.GetDirectories(rootPath,
                "*",
                SearchOption.AllDirectories);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        foreach (string directory in directories.OrderByDescending(path => path.Length))
        {
            DeleteDirectoryIfEmpty(directory);
        }

        DeleteDirectoryIfEmpty(rootPath);
    }

    private static void DeleteDirectoryIfEmpty(string path)
    {
        try
        {
            if (!Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path);
            }
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private ClientEngine GetEngine() => engine ?? throw new InvalidOperationException("The torrent engine has not been initialized.");
    private ManagedDownload GetDownload(string id) => downloads.TryGetValue(id, out ManagedDownload? value) ? value : throw new KeyNotFoundException("Torrent not found.");
    private static int DivideLimit(int limit, int divisor) => limit == 0 ? 0 : Math.Max(1, limit / Math.Max(1, divisor));
    private static int ToBytesPerSecond(int kilobytes) => kilobytes <= 0 ? 0 : (int)Math.Min(int.MaxValue, kilobytes * 1024L);
}
