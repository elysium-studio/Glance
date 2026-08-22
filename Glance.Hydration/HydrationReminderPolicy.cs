namespace Glance.Hydration;

public sealed class HydrationReminderPolicy
{
    public double GetExpectedProgress(HydrationSettings settings, DateTimeOffset now)
    {
        TimeSpan start = HydrationSettings.NormalizeReminderStart(settings.ReminderStart, settings.ReminderEnd);
        TimeSpan end = HydrationSettings.NormalizeReminderEnd(settings.ReminderEnd, start);
        TimeSpan current = now.TimeOfDay;

        if (current <= start)
        {
            return 0;
        }

        if (current >= end)
        {
            return 1;
        }

        return (current - start).TotalMinutes / (end - start).TotalMinutes;
    }

    public HydrationLevel GetLevel(HydrationSettings settings, HydrationSnapshot snapshot, DateTimeOffset now)
    {
        double progress = Math.Clamp(snapshot.ConsumedMillilitres / HydrationSettings.NormalizeDailyGoal(snapshot.DailyGoalMillilitres), 0, 1);

        if (progress >= 1)
        {
            return HydrationLevel.GoalReached;
        }

        double deficit = GetExpectedProgress(settings, now) - progress;
        return deficit <= 0.1 ? HydrationLevel.OnTrack : deficit <= 0.3 ? HydrationLevel.Behind : HydrationLevel.Critical;
    }

    public bool ShouldRemind(HydrationSettings settings, HydrationSnapshot snapshot, DateTimeOffset now)
    {
        if (!settings.RemindersEnabled || snapshot.ConsumedMillilitres >= HydrationSettings.NormalizeDailyGoal(snapshot.DailyGoalMillilitres))
        {
            return false;
        }

        TimeSpan start = HydrationSettings.NormalizeReminderStart(settings.ReminderStart, settings.ReminderEnd);
        TimeSpan end = HydrationSettings.NormalizeReminderEnd(settings.ReminderEnd, start);

        if (now.TimeOfDay < start || now.TimeOfDay > end)
        {
            return false;
        }

        TimeSpan interval = TimeSpan.FromMinutes(HydrationSettings.NormalizeReminderInterval(settings.ReminderIntervalMinutes));
        bool hasReminderToday = snapshot.LastReminderAt != default && snapshot.LastReminderAt.LocalDateTime.Date == now.LocalDateTime.Date;

        if (hasReminderToday && now - snapshot.LastReminderAt < interval)
        {
            return false;
        }

        DateTimeOffset firstReminder = new DateTimeOffset(now.Year, now.Month, now.Day, start.Hours, start.Minutes, 0, now.Offset).Add(interval);

        if (!hasReminderToday && now < firstReminder)
        {
            return false;
        }

        return !settings.OnlyRemindWhenBehind || GetLevel(settings, snapshot, now) is HydrationLevel.Behind or HydrationLevel.Critical;
    }
}
