using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;

namespace Glance.Timer;

public sealed partial class TimerViewModel : ObservableObject
{
    private TimeSpan adjustment;
    private TimeSpan duration;
    private TimeSpan remaining;
    private long lastTimestamp;

    public TimerViewModel(TimerSettings? settings = null)
    {
        TimerSettings initialSettings = settings ?? new TimerSettings();
        adjustment = GetAdjustment(initialSettings);
        duration = GetDefaultDuration(initialSettings);
        remaining = duration;
        remainingText = FormatTime(remaining);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleGlyph))]
    private bool isRunning;

    [ObservableProperty]
    private string remainingText;

    public bool CanDecreaseMinute => duration > adjustment;

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
        duration += adjustment;
        remaining += adjustment;
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
        duration -= adjustment;
        remaining -= adjustment;

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

    public void ApplySettings(TimerSettings settings)
    {
        adjustment = GetAdjustment(settings);

        if (!IsRunning)
        {
            duration = GetDefaultDuration(settings);
            remaining = duration;
            UpdateText();
        }

        OnPropertyChanged(nameof(CanDecreaseMinute));
    }

    private void UpdateText()
    {
        RemainingText = FormatTime(remaining);
    }

    private void RefreshIfRunning()
    {
        if (IsRunning)
        {
            Refresh();
        }
    }

    private static TimeSpan GetAdjustment(TimerSettings settings) =>
        TimeSpan.FromMinutes(Math.Clamp(settings.AdjustmentMinutes, 0.5, 60));

    private static TimeSpan GetDefaultDuration(TimerSettings settings) =>
        TimeSpan.FromMinutes(Math.Clamp(settings.DefaultDurationMinutes, 1, 1440));

    private static string FormatTime(TimeSpan value)
    {
        TimeSpan display = value < TimeSpan.Zero ? TimeSpan.Zero : value;
        return display.TotalHours >= 1
            ? $"{(int)display.TotalHours:00}:{display.Minutes:00}:{display.Seconds:00}"
            : $"{display.Minutes:00}:{display.Seconds:00}";
    }
}
