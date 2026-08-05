using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;

namespace Glance.Timer;

public sealed partial class TimerViewModel :
    ObservableObject
{
    private TimeSpan adjustment;
    private TimeSpan configuredDefaultDuration;
    private TimeSpan duration;
    private TimeSpan remaining;
    private long lastTimestamp;

    public TimerViewModel(TimerSettings? settings = null,
        DateTimeOffset? now = null)
    {
        TimerSettings initialSettings = settings ?? new TimerSettings();
        adjustment = GetAdjustment(initialSettings);
        configuredDefaultDuration = GetDefaultDuration(initialSettings);
        duration = configuredDefaultDuration;
        remaining = duration;

        if (initialSettings.ResumeAutomatically &&
            initialSettings.SessionDurationTicks > 0 &&
            initialSettings.SessionRemainingTicks >= 0)
        {
            duration = TimeSpan.FromTicks(initialSettings.SessionDurationTicks);
            remaining = TimeSpan.FromTicks(initialSettings.SessionRemainingTicks);

            if (initialSettings.SessionWasRunning)
            {
                DateTimeOffset current = now ?? DateTimeOffset.UtcNow;
                remaining -= current > initialSettings.SessionUpdatedUtc
                    ? current - initialSettings.SessionUpdatedUtc
                    : TimeSpan.Zero;

                if (remaining > TimeSpan.Zero)
                {
                    lastTimestamp = Stopwatch.GetTimestamp();
                    isRunning = true;
                }
                else
                {
                    remaining = TimeSpan.Zero;
                }
            }
        }

        remainingText = FormatTime(remaining);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleGlyph))]
    private bool isRunning;

    [ObservableProperty]
    private string remainingText;

    public bool CanDecreaseMinute => duration > adjustment;

    public string ToggleGlyph => IsRunning ? "\uF8AE" : "\uF5B0";

    public event EventHandler? SessionStateChanged;

    public void Pause()
    {
        if (IsRunning)
        {
            Toggle();
        }
    }

    public void Resume()
    {
        if (!IsRunning)
        {
            Toggle();
        }
    }

    public void Toggle()
    {
        if (IsRunning)
        {
            _ = Refresh();
            IsRunning = false;
        }
        else if (remaining > TimeSpan.Zero)
        {
            lastTimestamp = Stopwatch.GetTimestamp();
            IsRunning = true;
        }

        SessionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        IsRunning = false;
        remaining = duration;
        UpdateText();
        SessionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Start(TimeSpan newDuration)
    {
        duration = newDuration < TimeSpan.FromSeconds(1)
            ? TimeSpan.FromSeconds(1)
            : newDuration;
        remaining = duration;
        lastTimestamp = Stopwatch.GetTimestamp();
        IsRunning = true;
        UpdateText();
        OnPropertyChanged(nameof(CanDecreaseMinute));
        SessionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddMinute()
    {
        RefreshIfRunning();
        duration += adjustment;
        remaining += adjustment;
        UpdateText();
        OnPropertyChanged(nameof(CanDecreaseMinute));
        SessionStateChanged?.Invoke(this, EventArgs.Empty);
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
        SessionStateChanged?.Invoke(this, EventArgs.Empty);
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
            SessionStateChanged?.Invoke(this, EventArgs.Empty);
        }

        UpdateText();
        return completed;
    }

    public void ApplySettings(TimerSettings settings)
    {
        adjustment = GetAdjustment(settings);
        TimeSpan defaultDuration = GetDefaultDuration(settings);
        bool defaultDurationChanged = configuredDefaultDuration != defaultDuration;
        configuredDefaultDuration = defaultDuration;

        if (!IsRunning && defaultDurationChanged)
        {
            duration = configuredDefaultDuration;
            remaining = duration;
            UpdateText();
            SessionStateChanged?.Invoke(this, EventArgs.Empty);
        }

        OnPropertyChanged(nameof(CanDecreaseMinute));
    }

    public void WriteSessionState(TimerSettings settings,
        DateTimeOffset? now = null)
    {
        settings.SessionDurationTicks = duration.Ticks;
        settings.SessionRemainingTicks = remaining.Ticks;
        settings.SessionUpdatedUtc = now ?? DateTimeOffset.UtcNow;
        settings.SessionWasRunning = IsRunning;
    }

    private void UpdateText() => RemainingText = FormatTime(remaining);

    private void RefreshIfRunning()
    {
        if (IsRunning)
        {
            _ = Refresh();
        }
    }

    private static TimeSpan GetAdjustment(TimerSettings settings) => TimeSpan.FromMinutes(Math.Clamp(settings.AdjustmentMinutes, 0.5, 60));

    private static TimeSpan GetDefaultDuration(TimerSettings settings) => TimeSpan.FromMinutes(Math.Clamp(settings.DefaultDurationMinutes, 1, 1440));

    private static string FormatTime(TimeSpan value)
    {
        TimeSpan display = value < TimeSpan.Zero ? TimeSpan.Zero : value;
        return display.TotalHours >= 1
            ? $"{(int)display.TotalHours:00}:{display.Minutes:00}:{display.Seconds:00}"
            : $"{display.Minutes:00}:{display.Seconds:00}";
    }
}
