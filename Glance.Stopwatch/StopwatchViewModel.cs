using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.Stopwatch;

public sealed partial class StopwatchViewModel :
    ObservableObject
{
    private readonly System.Diagnostics.Stopwatch stopwatch = new();
    private TimeSpan elapsedOffset;

    public StopwatchViewModel(StopwatchSettings? settings = null,
        DateTimeOffset? now = null)
    {
        StopwatchSettings initialSettings = settings ?? new StopwatchSettings();

        if (initialSettings.ResumeAutomatically)
        {
            elapsedOffset = TimeSpan.FromTicks(Math.Max(0, initialSettings.SessionElapsedTicks));

            if (initialSettings.SessionWasRunning)
            {
                DateTimeOffset current = now ?? DateTimeOffset.UtcNow;
                elapsedOffset += current > initialSettings.SessionUpdatedUtc
                    ? current - initialSettings.SessionUpdatedUtc
                    : TimeSpan.Zero;
                stopwatch.Start();
                isRunning = true;
            }
        }

        Refresh();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleGlyph))]
    private bool isRunning;

    [ObservableProperty]
    private string elapsed = "00:00.00";

    public string ToggleGlyph => IsRunning ? "\uF8AE" : "\uF5B0";

    public event EventHandler? SessionStateChanged;

    public void Toggle()
    {
        if (IsRunning)
        {
            stopwatch.Stop();
            elapsedOffset += stopwatch.Elapsed;
            stopwatch.Reset();
            IsRunning = false;
        }
        else
        {
            stopwatch.Start();
            IsRunning = true;
        }

        Refresh();
        SessionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        stopwatch.Reset();
        elapsedOffset = TimeSpan.Zero;
        IsRunning = false;
        Refresh();
        SessionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Refresh()
    {
        TimeSpan value = elapsedOffset + stopwatch.Elapsed;
        Elapsed = value.TotalHours >= 1
            ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}.{value.Milliseconds / 10:00}"
            : $"{value.Minutes:00}:{value.Seconds:00}.{value.Milliseconds / 10:00}";
    }

    public void WriteSessionState(StopwatchSettings settings,
        DateTimeOffset? now = null)
    {
        settings.SessionElapsedTicks = (elapsedOffset + stopwatch.Elapsed).Ticks;
        settings.SessionUpdatedUtc = now ?? DateTimeOffset.UtcNow;
        settings.SessionWasRunning = IsRunning;
    }
}
