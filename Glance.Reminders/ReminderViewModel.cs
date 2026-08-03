using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;

namespace Glance.Reminders;

public sealed partial class ReminderViewModel(ITextLocalizer localizer) :
    ObservableObject
{
    private Func<ReminderItemViewModel?, Task>? editReminder;
    private Func<ReminderItemViewModel, Task>? removeReminder;

    [ObservableProperty]
    private bool hasReminders;

    [ObservableProperty]
    private ReminderItemViewModel? selectedReminder;

    public ObservableCollection<ReminderItemViewModel> Reminders { get; } = [];

    public string CompactText => SelectedReminder?.Title ?? localizer.GetText("EmptySummary");

    public string Title => localizer.GetText("ModuleTitle");

    public void ConfigureActions(Func<ReminderItemViewModel?, Task> edit,
        Func<ReminderItemViewModel, Task> remove)
    {
        editReminder = edit;
        removeReminder = remove;
    }

    public void Restore(IEnumerable<ReminderEntry> entries)
    {
        Reminders.Clear();

        foreach (ReminderEntry entry in Sort(entries))
        {
            Reminders.Add(CreateItem(entry));
        }

        SelectedReminder = Reminders.FirstOrDefault();
        UpdateState();
    }

    public ReminderItemViewModel Upsert(ReminderEntry entry)
    {
        ReminderItemViewModel? existing = Reminders.FirstOrDefault(item => item.Id == entry.Id);

        if (existing is null)
        {
            existing = CreateItem(entry);
        }
        else
        {
            Reminders.Remove(existing);
            existing.Update(entry);
        }

        int index = Reminders.TakeWhile(item => Compare(item.ToEntry(), entry) <= 0).Count();
        Reminders.Insert(index, existing);
        SelectedReminder = existing;
        UpdateState();
        return existing;
    }

    public void Remove(ReminderItemViewModel item)
    {
        int index = Reminders.IndexOf(item);

        if (index < 0)
        {
            return;
        }

        Reminders.RemoveAt(index);
        SelectedReminder = Reminders.Count == 0 ? null : Reminders[Math.Min(index, Reminders.Count - 1)];
        UpdateState();
    }

    [RelayCommand]
    private Task AddAsync() => editReminder?.Invoke(null) ?? Task.CompletedTask;

    partial void OnSelectedReminderChanged(ReminderItemViewModel? value) =>
        OnPropertyChanged(nameof(CompactText));

    private void UpdateState()
    {
        HasReminders = Reminders.Count > 0;
        OnPropertyChanged(nameof(CompactText));
    }

    private ReminderItemViewModel CreateItem(ReminderEntry entry) =>
        new(entry, localizer, EditAsync, RemoveAsync);

    private Task EditAsync(ReminderItemViewModel item) =>
        editReminder?.Invoke(item) ?? Task.CompletedTask;

    private Task RemoveAsync(ReminderItemViewModel item) =>
        removeReminder?.Invoke(item) ?? Task.CompletedTask;

    private static IEnumerable<ReminderEntry> Sort(IEnumerable<ReminderEntry> entries) =>
        entries.OrderBy(entry => entry.DueAt).ThenByDescending(entry => entry.Priority).ThenBy(entry => entry.CreatedAt);

    private static int Compare(ReminderEntry left,
        ReminderEntry right)
    {
        int dueComparison = left.DueAt.CompareTo(right.DueAt);
        return dueComparison != 0 ? dueComparison : right.Priority.CompareTo(left.Priority);
    }
}
