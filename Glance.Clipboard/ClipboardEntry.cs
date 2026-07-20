using Glance.Application.Abstractions;
using System.Globalization;

namespace Glance.Clipboard;

public sealed record ClipboardEntry(
    string Id,
    string Preview,
    string KindLabel,
    string Glyph,
    DateTimeOffset Timestamp,
    ITextLocalizer Localizer)
{
    public string TimeText
    {
        get
        {
            TimeSpan age = DateTimeOffset.Now - Timestamp;

            return age switch
            {
                { TotalMinutes: < 1 } => Localizer.GetText("TimeNow"),
                { TotalHours: < 1 } => $"{Math.Max(1, (int)age.TotalMinutes)}m",
                { TotalDays: < 1 } => $"{Math.Max(1, (int)age.TotalHours)}h",
                _ => Timestamp.ToString("d MMM", CultureInfo.CurrentCulture)
            };
        }
    }
}
