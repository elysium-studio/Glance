namespace Glance.Torrents;

public static class TorrentSeedingPolicy
{
    public static bool ShouldStop(TorrentSettings settings, long downloadedBytes, long uploadedBytes, TimeSpan seedingTime)
    {
        if (!settings.ContinueSeeding)
        {
            return true;
        }

        bool ratioEnabled = settings.SeedingRatioLimit > 0;
        bool timeEnabled = settings.SeedingTimeLimitMinutes > 0;

        if (!ratioEnabled && !timeEnabled)
        {
            return false;
        }

        bool ratioReached = ratioEnabled && downloadedBytes > 0 && uploadedBytes / (double)downloadedBytes >= settings.SeedingRatioLimit;
        bool timeReached = timeEnabled && seedingTime >= TimeSpan.FromMinutes(settings.SeedingTimeLimitMinutes);

        return settings.SeedingLimitMode == TorrentSeedingLimitMode.Either
            ? ratioReached || timeReached
            : (!ratioEnabled || ratioReached) && (!timeEnabled || timeReached);
    }
}
