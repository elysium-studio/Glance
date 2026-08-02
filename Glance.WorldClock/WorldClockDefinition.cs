namespace Glance.WorldClock;

public sealed record WorldClockDefinition(string Id,
    string DisplayName,
    TimeZoneInfo TimeZone);
