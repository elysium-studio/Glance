using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;

namespace Glance.Timer;

public partial class TimerViewModel : ObservableObject
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    private TimeSpan duration = TimeSpan.FromMinutes(5);
    private TimeSpan remaining = TimeSpan.FromMinutes(5);
    private long lastTimestamp;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleGlyph))]
    private bool isRunning;

    [ObservableProperty]
    private string remainingText = "05:00";

    public bool CanDecreaseMinute => duration > Minute;

    public string ToggleGlyph => IsRunning ? "\uF8AE" : "\uF5B0";

    public void Toggle()
    {
        if (IsRunning)
        {
            Refresh();
            IsRunning = false;
        }
        else if (remaining > TimeSpan.Zero)
        {
            lastTimestamp = Stopwatch.GetTimestamp();
            IsRunning = true;
        }
    }

    public void Reset()
    {
        IsRunning = false;
        remaining = duration;
        UpdateText();
    }

    public void AddMinute()
    {
        RefreshIfRunning();
        duration += Minute;
        remaining += Minute;
        UpdateText();
        OnPropertyChanged(nameof(CanDecreaseMinute));
    }

    public void DecreaseMinute()
    {
        if (!CanDecreaseMinute)
        {
            return;
        }

        RefreshIfRunning();
        duration -= Minute;
        remaining -= Minute;

        if (remaining <= TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
            IsRunning = false;
        }

        UpdateText();
        OnPropertyChanged(nameof(CanDecreaseMinute));
    }

    public bool Refresh()
    {
        if (!IsRunning)
        {
            return false;
        }

        long timestamp = Stopwatch.GetTimestamp();
        remaining -= Stopwatch.GetElapsedTime(lastTimestamp, timestamp);
        lastTimestamp = timestamp;

        bool completed = remaining <= TimeSpan.Zero;

        if (completed)
        {
            remaining = TimeSpan.Zero;
            IsRunning = false;
        }

        UpdateText();
        return completed;
    }

    private void UpdateText()
    {
        TimeSpan display = remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        RemainingText = display.TotalHours >= 1
            ? $"{(int)display.TotalHours:00}:{display.Minutes:00}:{display.Seconds:00}"
            : $"{display.Minutes:00}:{display.Seconds:00}";
    }

    private void RefreshIfRunning()
    {
        if (IsRunning)
        {
            Refresh();
        }
    }
}
