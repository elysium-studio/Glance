using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;

namespace Glance.Torrents.WinUI;

public sealed partial class TorrentSettingsViewModel : ObservableObject, IGlanceModuleSettingViewModel
{
    private readonly IWritableOptions<TorrentSettings> writer;
    private readonly SemaphoreSlim saveSynchronization = new(1, 1);
    private bool initialized;
    private int saveQueued;
    private int disposed;
    [ObservableProperty] public partial string DownloadPath { get; set; }
    [ObservableProperty] public partial int MaximumDownloadKilobytesPerSecond { get; set; }
    [ObservableProperty] public partial int MaximumUploadKilobytesPerSecond { get; set; }
    [ObservableProperty] public partial int MaximumPeersPerTorrent { get; set; }
    [ObservableProperty] public partial bool ContinueSeeding { get; set; }
    [ObservableProperty] public partial double SeedingRatioLimit { get; set; }
    [ObservableProperty] public partial int SeedingTimeLimitMinutes { get; set; }
    [ObservableProperty] public partial int SeedingLimitModeIndex { get; set; }
    [ObservableProperty] public partial bool HasDownloadPathValidationError { get; set; }
    [ObservableProperty] public partial string DownloadPathValidationMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasTransferValidationError { get; set; }
    [ObservableProperty] public partial string TransferValidationMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasSeedingValidationError { get; set; }
    [ObservableProperty] public partial string SeedingValidationMessage { get; set; } = string.Empty;

    public TorrentSettingsViewModel(TorrentSettings settings, IWritableOptions<TorrentSettings> writer)
    {
        this.writer = writer;
        TorrentSettings normalized = TorrentSettings.Normalize(settings);
        DownloadPath = TorrentComponent.ResolveDownloadPath(normalized);
        MaximumDownloadKilobytesPerSecond = normalized.MaximumDownloadKilobytesPerSecond;
        MaximumUploadKilobytesPerSecond = normalized.MaximumUploadKilobytesPerSecond;
        MaximumPeersPerTorrent = normalized.MaximumPeersPerTorrent;
        ContinueSeeding = normalized.ContinueSeeding;
        SeedingRatioLimit = normalized.SeedingRatioLimit;
        SeedingTimeLimitMinutes = normalized.SeedingTimeLimitMinutes;
        SeedingLimitModeIndex = normalized.SeedingLimitMode == TorrentSeedingLimitMode.Both ? 1 : 0;
        initialized = true;
    }

    public string ModuleId => "Torrent";
    public int Order => 10;

    private async Task SaveQueuedAsync()
    {
        await saveSynchronization.WaitAsync();

        try
        {
            while (Interlocked.Exchange(ref saveQueued, 0) != 0 && Volatile.Read(ref disposed) == 0)
            {
                Validate();

                if (HasDownloadPathValidationError || HasTransferValidationError || HasSeedingValidationError)
                {
                    continue;
                }

                string downloadPath = DownloadPath;
                int maximumDownload = MaximumDownloadKilobytesPerSecond;
                int maximumUpload = MaximumUploadKilobytesPerSecond;
                int maximumPeers = MaximumPeersPerTorrent;
                bool continueSeeding = ContinueSeeding;
                double ratioLimit = SeedingRatioLimit;
                int timeLimit = SeedingTimeLimitMinutes;
                TorrentSeedingLimitMode limitMode = SeedingLimitModeIndex == 1
                    ? TorrentSeedingLimitMode.Both
                    : TorrentSeedingLimitMode.Either;
                await writer.WriteAsync(settings =>
                {
                    settings.DefaultDownloadPath = downloadPath;
                    settings.MaximumDownloadKilobytesPerSecond = maximumDownload;
                    settings.MaximumUploadKilobytesPerSecond = maximumUpload;
                    settings.MaximumPeersPerTorrent = maximumPeers;
                    settings.ContinueSeeding = continueSeeding;
                    settings.SeedingRatioLimit = ratioLimit;
                    settings.SeedingTimeLimitMinutes = timeLimit;
                    settings.SeedingLimitMode = limitMode;
                });
            }
        }
        finally
        {
            _ = saveSynchronization.Release();
        }
    }

    private void Validate()
    {
        DownloadPathValidationMessage = string.IsNullOrWhiteSpace(DownloadPath)
            ? "Choose a download location."
            : string.Empty;
        TransferValidationMessage = MaximumDownloadKilobytesPerSecond < 0 ||
            MaximumUploadKilobytesPerSecond < 0 ||
            MaximumPeersPerTorrent < 0
            ? "Transfer limits cannot be negative."
            : MaximumPeersPerTorrent > 10_000
                ? "Connections per download must be 10,000 or less."
                : string.Empty;
        SeedingValidationMessage = SeedingRatioLimit < 0 || SeedingTimeLimitMinutes < 0
            ? "Seeding limits cannot be negative."
            : string.Empty;
        HasDownloadPathValidationError = DownloadPathValidationMessage.Length > 0;
        HasTransferValidationError = TransferValidationMessage.Length > 0;
        HasSeedingValidationError = SeedingValidationMessage.Length > 0;
    }

    private void QueueSave()
    {
        if (!initialized || Volatile.Read(ref disposed) != 0)
        {
            return;
        }

        _ = Interlocked.Exchange(ref saveQueued, 1);
        _ = SaveQueuedAsync();
    }

    partial void OnDownloadPathChanged(string value) => QueueSave();
    partial void OnMaximumDownloadKilobytesPerSecondChanged(int value) => QueueSave();
    partial void OnMaximumUploadKilobytesPerSecondChanged(int value) => QueueSave();
    partial void OnMaximumPeersPerTorrentChanged(int value) => QueueSave();
    partial void OnContinueSeedingChanged(bool value) => QueueSave();
    partial void OnSeedingRatioLimitChanged(double value) => QueueSave();
    partial void OnSeedingTimeLimitMinutesChanged(int value) => QueueSave();
    partial void OnSeedingLimitModeIndexChanged(int value) => QueueSave();

    public void Dispose() => _ = Interlocked.Exchange(ref disposed, 1);
}
