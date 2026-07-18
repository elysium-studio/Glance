using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;

namespace Glance.Timer;

public partial class TimerViewModel : ObservableObject
{
    private TimeSpan duration = TimeSpan.FromMinutes(5);
    private TimeSpan remaining = TimeSpan.FromMinutes(5);
    private long lastTimestamp;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleText))]
    private bool isRunning;

    [ObservableProperty]
    private string remainingText = "05:00";

    public string ToggleText => IsRunning ? "Pause" : "Start";

    [RelayCommand]
    private void Toggle()
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

    [RelayCommand]
    private void Reset()
    {
        IsRunning = false;
        remaining = duration;
        UpdateText();
    }

    [RelayCommand]
    private void AddMinute()
    {
        duration += TimeSpan.FromMinutes(1);
        remaining += TimeSpan.FromMinutes(1);
        UpdateText();
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
}
