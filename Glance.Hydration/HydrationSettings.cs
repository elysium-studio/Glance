namespace Glance.Hydration;

public sealed class HydrationSettings
{
    public double DailyGoalMillilitres { get; set; } = 2000;

    public double ServingSizeMillilitres { get; set; } = 250;

    public bool RemindersEnabled { get; set; } = true;

    public double ReminderIntervalMinutes { get; set; } = 60;

    public TimeSpan ReminderStart { get; set; } = TimeSpan.FromHours(8);

    public TimeSpan ReminderEnd { get; set; } = TimeSpan.FromHours(22);

    public bool OnlyRemindWhenBehind { get; set; } = true;

    public string TrackingDate { get; set; } = string.Empty;

    public double ConsumedMillilitres { get; set; }

    public double LastServingMillilitres { get; set; }

    public DateTimeOffset LastServingAt { get; set; }

    public DateTimeOffset LastReminderAt { get; set; }

    public static double NormalizeDailyGoal(double value) => Math.Clamp(value, 500, 6000);

    public static double NormalizeServingSize(double value) => Math.Clamp(value, 50, 1000);

    public static double NormalizeReminderInterval(double value) => Math.Clamp(value, 15, 240);

    public static TimeSpan NormalizeReminderStart(TimeSpan value, TimeSpan reminderEnd) => Clamp(value, TimeSpan.Zero, NormalizeReminderEnd(reminderEnd, TimeSpan.Zero) - TimeSpan.FromMinutes(15));

    public static TimeSpan NormalizeReminderEnd(TimeSpan value, TimeSpan reminderStart) => Clamp(value, Clamp(reminderStart, TimeSpan.Zero, TimeSpan.FromHours(23.5)) + TimeSpan.FromMinutes(15), TimeSpan.FromHours(23.75));

    private static TimeSpan Clamp(TimeSpan value, TimeSpan minimum, TimeSpan maximum) => value < minimum ? minimum : value > maximum ? maximum : value;
}
