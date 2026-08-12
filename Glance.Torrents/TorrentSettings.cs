using System.Text.Json.Serialization;

namespace Glance.Torrents;

public sealed class TorrentSettings
{
    public string? DefaultDownloadPath { get; set; }

    public int MaximumDownloadKilobytesPerSecond { get; set; }

    public int MaximumUploadKilobytesPerSecond { get; set; }

    public int MaximumPeersPerTorrent { get; set; } = 80;

    public bool ContinueSeeding { get; set; } = true;

    public double SeedingRatioLimit { get; set; }

    public int SeedingTimeLimitMinutes { get; set; }

    public TorrentSeedingLimitMode SeedingLimitMode { get; set; } = TorrentSeedingLimitMode.Either;

    public bool RequestAttentionOnCompletion { get; set; }

    public static TorrentSettings Normalize(TorrentSettings settings) => new()
    {
        DefaultDownloadPath = string.IsNullOrWhiteSpace(settings.DefaultDownloadPath) ? null : settings.DefaultDownloadPath.Trim(),
        MaximumDownloadKilobytesPerSecond = Math.Max(0, settings.MaximumDownloadKilobytesPerSecond),
        MaximumUploadKilobytesPerSecond = Math.Max(0, settings.MaximumUploadKilobytesPerSecond),
        MaximumPeersPerTorrent = Math.Clamp(settings.MaximumPeersPerTorrent, 0, 10_000),
        ContinueSeeding = settings.ContinueSeeding,
        SeedingRatioLimit = Math.Max(0, settings.SeedingRatioLimit),
        SeedingTimeLimitMinutes = Math.Max(0, settings.SeedingTimeLimitMinutes),
        SeedingLimitMode = Enum.IsDefined(settings.SeedingLimitMode) ? settings.SeedingLimitMode : TorrentSeedingLimitMode.Either,
        RequestAttentionOnCompletion = settings.RequestAttentionOnCompletion
    };
}

[JsonSerializable(typeof(TorrentSettings))]
[JsonSerializable(typeof(TorrentStateDocument))]
public sealed partial class TorrentJsonContext : JsonSerializerContext;
