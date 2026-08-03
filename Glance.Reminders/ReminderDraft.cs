namespace Glance.Reminders;

public sealed record ReminderDraft(string Title,
    DateTimeOffset DueAt,
    ReminderPriority Priority);
