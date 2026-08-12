using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;

namespace Glance.Torrents.WinUI;

public sealed partial class TorrentSettingsViewModel : ObservableObject, IGlanceModuleSettingViewModel
{
    private readonly IWritableOptions<TorrentSettings> writer;
    [ObservableProperty] public partial string DownloadPath { get; set; }
    [ObservableProperty] public partial int MaximumDownloadKilobytesPerSecond { get; set; }
    [ObservableProperty] public partial int MaximumUploadKilobytesPerSecond { get; set; }
    [ObservableProperty] public partial int MaximumPeersPerTorrent { get; set; }
    [ObservableProperty] public partial bool ContinueSeeding { get; set; }
    [ObservableProperty] public partial double SeedingRatioLimit { get; set; }
    [ObservableProperty] public partial int SeedingTimeLimitMinutes { get; set; }
    [ObservableProperty] public partial int SeedingLimitModeIndex { get; set; }
    [ObservableProperty] public partial bool RequestAttentionOnCompletion { get; set; }
    [ObservableProperty] public partial string? ValidationMessage { get; set; }

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
        RequestAttentionOnCompletion = normalized.RequestAttentionOnCompletion;
    }

    public string ModuleId => "Torrent";
    public int Order => 10;

    public async Task<bool> SaveAsync()
    {
        if (MaximumDownloadKilobytesPerSecond < 0 || MaximumUploadKilobytesPerSecond < 0 || MaximumPeersPerTorrent < 0 || SeedingRatioLimit < 0 || SeedingTimeLimitMinutes < 0)
        {
            ValidationMessage = "Limits cannot be negative.";
            return false;
        }
        if (MaximumPeersPerTorrent > 10_000)
        {
            ValidationMessage = "Connections per download must be 10,000 or less.";
            return false;
        }
        ValidationMessage = null;
        await writer.WriteAsync(settings =>
        {
            settings.DefaultDownloadPath = DownloadPath;
            settings.MaximumDownloadKilobytesPerSecond = MaximumDownloadKilobytesPerSecond;
            settings.MaximumUploadKilobytesPerSecond = MaximumUploadKilobytesPerSecond;
            settings.MaximumPeersPerTorrent = MaximumPeersPerTorrent;
            settings.ContinueSeeding = ContinueSeeding;
            settings.SeedingRatioLimit = SeedingRatioLimit;
            settings.SeedingTimeLimitMinutes = SeedingTimeLimitMinutes;
            settings.SeedingLimitMode = SeedingLimitModeIndex == 1 ? TorrentSeedingLimitMode.Both : TorrentSeedingLimitMode.Either;
            settings.RequestAttentionOnCompletion = RequestAttentionOnCompletion;
        });
        return true;
    }

    public void Dispose() { }
}
