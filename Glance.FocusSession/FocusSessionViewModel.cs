using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;

namespace Glance.FocusSession;

public partial class FocusSessionViewModel : ObservableObject
{
    private TimeSpan breakDuration;
    private TimeSpan focusDuration;
    private long lastTimestamp;
    private TimeSpan remaining;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleGlyph))]
    private bool isRunning;

    [ObservableProperty]
    private FocusSessionPhase phase;

    [ObservableProperty]
    private double progress;

    [ObservableProperty]
    private string remainingText;

    [ObservableProperty]
    private int completedFocusSessions;

    public FocusSessionViewModel(
        TimeSpan? focusDuration = null,
        TimeSpan? breakDuration = null)
    {
        this.focusDuration = focusDuration ?? TimeSpan.FromMinutes(25);
        this.breakDuration = breakDuration ?? TimeSpan.FromMinutes(5);

        if (this.focusDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(focusDuration));
        }

        if (this.breakDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(breakDuration));
        }

        phase = FocusSessionPhase.Focus;
        remaining = this.focusDuration;
        remainingText = FormatTime(remaining);
    }

    public string ToggleGlyph => IsRunning ? "\uF8AE" : "\uF5B0";

    public void Toggle()
    {
        if (IsRunning)
        {
            Refresh();
            IsRunning = false;
            return;
        }

        lastTimestamp = Stopwatch.GetTimestamp();
        IsRunning = true;
    }

    public void Reset()
    {
        IsRunning = false;
        remaining = GetDuration(Phase);
        UpdateDisplay();
    }

    public void Skip()
    {
        IsRunning = false;
        Phase = GetNextPhase(Phase);
        remaining = GetDuration(Phase);
        UpdateDisplay();
    }

    public FocusSessionPhase? Refresh()
    {
        if (!IsRunning)
        {
            return null;
        }

        long timestamp = Stopwatch.GetTimestamp();
        remaining -= Stopwatch.GetElapsedTime(lastTimestamp, timestamp);
        lastTimestamp = timestamp;

        if (remaining > TimeSpan.Zero)
        {
            UpdateDisplay();
            return null;
        }

        FocusSessionPhase completedPhase = Phase;

        if (completedPhase == FocusSessionPhase.Focus)
        {
            CompletedFocusSessions++;
        }

        IsRunning = false;
        Phase = GetNextPhase(completedPhase);
        remaining = GetDuration(Phase);
        UpdateDisplay();
        return completedPhase;
    }

    public void ApplySettings(FocusSessionSettings settings)
    {
        focusDuration = TimeSpan.FromMinutes(Math.Clamp(settings.FocusDurationMinutes, 1, 180));
        breakDuration = TimeSpan.FromMinutes(Math.Clamp(settings.BreakDurationMinutes, 1, 60));

        if (!IsRunning)
        {
            remaining = GetDuration(Phase);
            UpdateDisplay();
        }
    }

    private static FocusSessionPhase GetNextPhase(FocusSessionPhase phase) =>
        phase == FocusSessionPhase.Focus
            ? FocusSessionPhase.Break
            : FocusSessionPhase.Focus;

    private static string FormatTime(TimeSpan value)
    {
        TimeSpan display = value < TimeSpan.Zero ? TimeSpan.Zero : value;

        return display.TotalHours >= 1
            ? $"{(int)display.TotalHours:00}:{display.Minutes:00}:{display.Seconds:00}"
            : $"{display.Minutes:00}:{display.Seconds:00}";
    }

    private TimeSpan GetDuration(FocusSessionPhase value) =>
        value == FocusSessionPhase.Focus
            ? focusDuration
            : breakDuration;

    private void UpdateDisplay()
    {
        TimeSpan duration = GetDuration(Phase);
        double elapsed = Math.Clamp((duration - remaining).TotalMilliseconds, 0, duration.TotalMilliseconds);

        RemainingText = FormatTime(remaining);
        Progress = elapsed / duration.TotalMilliseconds * 100;
    }
}
