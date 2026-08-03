namespace Glance.Reminders.Tests;

public sealed class ReminderAttentionTrackerTests
{
    [Fact]
    public void InitialSnapshotDoesNotRequestAttention()
    {
        DateTimeOffset now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
        ReminderEntry reminder = new("one", "Dentist", now.AddMinutes(10), ReminderPriority.High, now);
        ReminderAttentionTracker tracker = new();

        tracker.Initialize([reminder], now);

        Assert.Empty(tracker.Update([reminder], now));
    }

    [Fact]
    public void HighPriorityReminderApproachesThirtyMinutesBeforeDue()
    {
        DateTimeOffset now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
        ReminderEntry reminder = new("one", "Dentist", now.AddMinutes(31), ReminderPriority.High, now);
        ReminderAttentionTracker tracker = new();
        tracker.Initialize([reminder], now);

        ReminderAttentionChange change = Assert.Single(tracker.Update([reminder], now.AddMinutes(2)));

        Assert.Equal(ReminderAttentionState.Approaching, change.State);
    }

    [Fact]
    public void DueReminderEscalatesAfterApproaching()
    {
        DateTimeOffset now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
        ReminderEntry reminder = new("one", "Dentist", now.AddMinutes(4), ReminderPriority.Low, now);
        ReminderAttentionTracker tracker = new();
        tracker.Initialize([reminder], now);

        ReminderAttentionChange change = Assert.Single(tracker.Update([reminder], now.AddMinutes(5)));

        Assert.Equal(ReminderAttentionState.Due, change.State);
    }
}
