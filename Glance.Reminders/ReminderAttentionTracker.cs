namespace Glance.Reminders;

public sealed class ReminderAttentionTracker
{
    private readonly Dictionary<string, ReminderAttentionState> states = [];

    public void Initialize(IEnumerable<ReminderEntry> reminders,
        DateTimeOffset now)
    {
        states.Clear();

        foreach (ReminderEntry reminder in reminders)
        {
            states[reminder.Id] = GetState(reminder, now);
        }
    }

    public void Track(ReminderEntry reminder,
        DateTimeOffset now) =>
        states[reminder.Id] = GetState(reminder, now);

    public void Remove(string id) => states.Remove(id);

    public IReadOnlyList<ReminderAttentionChange> Update(IEnumerable<ReminderEntry> reminders,
        DateTimeOffset now)
    {
        List<ReminderAttentionChange> changes = [];
        HashSet<string> activeIds = [];

        foreach (ReminderEntry reminder in reminders)
        {
            activeIds.Add(reminder.Id);
            ReminderAttentionState current = GetState(reminder, now);

            if (!states.TryGetValue(reminder.Id, out ReminderAttentionState previous))
            {
                states[reminder.Id] = current;
                continue;
            }

            if (current > previous)
            {
                changes.Add(new ReminderAttentionChange(reminder, current));
                states[reminder.Id] = current;
            }
        }

        foreach (string id in states.Keys.Where(id => !activeIds.Contains(id)).ToArray())
        {
            states.Remove(id);
        }

        return changes;
    }

    private static ReminderAttentionState GetState(ReminderEntry reminder,
        DateTimeOffset now)
    {
        if (reminder.DueAt <= now)
        {
            return ReminderAttentionState.Due;
        }

        TimeSpan leadTime = reminder.Priority switch
        {
            ReminderPriority.High => TimeSpan.FromMinutes(30),
            ReminderPriority.Normal => TimeSpan.FromMinutes(15),
            _ => TimeSpan.FromMinutes(5)
        };
        return reminder.DueAt - now <= leadTime ? ReminderAttentionState.Approaching : ReminderAttentionState.Upcoming;
    }
}

public sealed record ReminderAttentionChange(ReminderEntry Reminder,
    ReminderAttentionState State);

public enum ReminderAttentionState
{
    Upcoming,
    Approaching,
    Due
}
