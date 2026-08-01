using CommunityToolkit.Mvvm.ComponentModel;
using System.Globalization;

namespace Glance.WorldClock;

public sealed partial class WorldClockItemViewModel(WorldClockDefinition definition) :
    ObservableObject
{
    public string Id => definition.Id;

    public string DisplayName => definition.DisplayName;

    [ObservableProperty]
    private string dateText = string.Empty;

    [ObservableProperty]
    private string timeText = string.Empty;

    public void Refresh(DateTimeOffset utcNow, bool use24HourTime)
    {
        DateTimeOffset localTime = TimeZoneInfo.ConvertTime(utcNow, definition.TimeZone);
        TimeText = localTime.ToString(use24HourTime ? "HH:mm" : "h:mm tt", CultureInfo.CurrentCulture);
        DateText = localTime.ToString("ddd, d MMM", CultureInfo.CurrentCulture);
    }
}
