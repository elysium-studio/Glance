namespace Glance.WorldClock;

public sealed class WorldClockSettings
{
    public bool Use24HourTime { get; set; } = true;

    public List<string> TimeZoneIds { get; set; } = [];
}
