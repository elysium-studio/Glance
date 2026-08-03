namespace Glance.Reminders.Tests;

public sealed class ReminderRepositoryTests :
    IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Glance.Reminders.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveOrdersRemindersByDueDateThenPriority()
    {
        ReminderRepository repository = new(Path.Combine(directory, "reminders.db"));
        DateTimeOffset createdAt = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
        repository.Save(new ReminderEntry("normal", "Normal", createdAt.AddHours(2), ReminderPriority.Normal, createdAt));
        repository.Save(new ReminderEntry("low", "Low", createdAt.AddHours(1), ReminderPriority.Low, createdAt));
        repository.Save(new ReminderEntry("high", "High", createdAt.AddHours(1), ReminderPriority.High, createdAt));

        IReadOnlyList<ReminderEntry> reminders = repository.Load();

        Assert.Equal(["high", "low", "normal"], reminders.Select(reminder => reminder.Id));
    }

    [Fact]
    public void RemoveDeletesReminder()
    {
        ReminderRepository repository = new(Path.Combine(directory, "reminders.db"));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        repository.Save(new ReminderEntry("one", "One", now.AddHours(1), ReminderPriority.Normal, now));

        repository.Remove("one");

        Assert.Empty(repository.Load());
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
