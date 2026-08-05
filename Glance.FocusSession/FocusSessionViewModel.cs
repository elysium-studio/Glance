using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;

namespace Glance.FocusSession;

public sealed partial class FocusSessionViewModel :
    ObservableObject
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

    public FocusSessionViewModel(TimeSpan? focusDuration = null,
        TimeSpan? breakDuration = null,
        FocusSessionSettings? settings = null,
        DateTimeOffset? now = null)
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

        if (settings is { ResumeAutomatically: true, SessionRemainingTicks: > 0 } &&
            Enum.IsDefined(settings.SessionPhase))
        {
            phase = settings.SessionPhase;
            remaining = TimeSpan.FromTicks(settings.SessionRemainingTicks);
            completedFocusSessions = Math.Max(0, settings.SessionCompletedFocusSessions);

            if (settings.SessionWasRunning)
            {
                DateTimeOffset current = now ?? DateTimeOffset.UtcNow;
                remaining -= current > settings.SessionUpdatedUtc
                    ? current - settings.SessionUpdatedUtc
                    : TimeSpan.Zero;

                if (remaining > TimeSpan.Zero)
                {
                    lastTimestamp = Stopwatch.GetTimestamp();
                    isRunning = true;
                }
                else
                {
                    if (phase == FocusSessionPhase.Focus)
                    {
                        completedFocusSessions++;
                    }

                    phase = GetNextPhase(phase);
                    remaining = GetDuration(phase);
                }
            }
        }

        remainingText = FormatTime(remaining);
        UpdateProgress();
    }

    public string ToggleGlyph => IsRunning ? "\uF8AE" : "\uF5B0";

    public event EventHandler? SessionStateChanged;

    public void Toggle()
    {
        if (IsRunning)
        {
            _ = Refresh();
            IsRunning = false;
            SessionStateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        lastTimestamp = Stopwatch.GetTimestamp();
        IsRunning = true;
        SessionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        IsRunning = false;
        remaining = GetDuration(Phase);
        UpdateDisplay();
        SessionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Skip()
    {
        IsRunning = false;
        Phase = GetNextPhase(Phase);
        remaining = GetDuration(Phase);
        UpdateDisplay();
        SessionStateChanged?.Invoke(this, EventArgs.Empty);
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
        SessionStateChanged?.Invoke(this, EventArgs.Empty);
        return completedPhase;
    }

    public void ApplySettings(FocusSessionSettings settings)
    {
        TimeSpan updatedFocusDuration = TimeSpan.FromMinutes(Math.Clamp(settings.FocusDurationMinutes, 1, 180));
        TimeSpan updatedBreakDuration = TimeSpan.FromMinutes(Math.Clamp(settings.BreakDurationMinutes, 1, 60));
        bool durationsChanged = focusDuration != updatedFocusDuration || breakDuration != updatedBreakDuration;
        focusDuration = updatedFocusDuration;
        breakDuration = updatedBreakDuration;

        if (!IsRunning && durationsChanged)
        {
            remaining = GetDuration(Phase);
            UpdateDisplay();
            SessionStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void WriteSessionState(FocusSessionSettings settings,
        DateTimeOffset? now = null)
    {
        settings.SessionCompletedFocusSessions = CompletedFocusSessions;
        settings.SessionPhase = Phase;
        settings.SessionRemainingTicks = remaining.Ticks;
        settings.SessionUpdatedUtc = now ?? DateTimeOffset.UtcNow;
        settings.SessionWasRunning = IsRunning;
    }

    private static FocusSessionPhase GetNextPhase(FocusSessionPhase phase) => phase == FocusSessionPhase.Focus
            ? FocusSessionPhase.Break
            : FocusSessionPhase.Focus;

    private static string FormatTime(TimeSpan value)
    {
        TimeSpan display = value < TimeSpan.Zero ? TimeSpan.Zero : value;

        return display.TotalHours >= 1
            ? $"{(int)display.TotalHours:00}:{display.Minutes:00}:{display.Seconds:00}"
            : $"{display.Minutes:00}:{display.Seconds:00}";
    }

    private TimeSpan GetDuration(FocusSessionPhase value) => value == FocusSessionPhase.Focus
            ? focusDuration
            : breakDuration;

    private void UpdateDisplay()
    {
        RemainingText = FormatTime(remaining);
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        TimeSpan duration = GetDuration(Phase);
        double elapsed = Math.Clamp((duration - remaining).TotalMilliseconds, 0, duration.TotalMilliseconds);
        Progress = elapsed / duration.TotalMilliseconds * 100;
    }
}
