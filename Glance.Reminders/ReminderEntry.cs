namespace Glance.Reminders;

public sealed record ReminderEntry(string Id,
    string Title,
    DateTimeOffset DueAt,
    ReminderPriority Priority,
    DateTimeOffset CreatedAt);
