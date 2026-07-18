namespace Glance.Clipboard;

public sealed record ClipboardEntry(
    string Id,
    string Preview,
    string KindLabel,
    string Glyph,
    DateTimeOffset Timestamp)
{
    public string TimeText
    {
        get
        {
            TimeSpan age = DateTimeOffset.Now - Timestamp;

            return age switch
            {
                { TotalMinutes: < 1 } => "Now",
                { TotalHours: < 1 } => $"{Math.Max(1, (int)age.TotalMinutes)}m",
                { TotalDays: < 1 } => $"{Math.Max(1, (int)age.TotalHours)}h",
                _ => Timestamp.ToString("d MMM")
            };
        }
    }
}
