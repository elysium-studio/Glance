namespace Glance.Hydration;

public sealed record HydrationSnapshot(double ConsumedMillilitres, double DailyGoalMillilitres, DateTimeOffset LastReminderAt);
