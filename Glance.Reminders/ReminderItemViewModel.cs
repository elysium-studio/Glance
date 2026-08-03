using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Glance.Application.Abstractions;
using System.Globalization;

namespace Glance.Reminders;

public sealed partial class ReminderItemViewModel(ReminderEntry entry,
    ITextLocalizer localizer,
    Func<ReminderItemViewModel, Task> edit,
    Func<ReminderItemViewModel, Task> remove) :
    ObservableObject
{
    private ReminderEntry entry = entry;

    public string Id => entry.Id;

    public string Title => entry.Title;

    public DateTimeOffset DueAt => entry.DueAt;

    public ReminderPriority Priority => entry.Priority;

    public DateTimeOffset CreatedAt => entry.CreatedAt;

    public string DueText => DueAt.LocalDateTime.ToString("g", CultureInfo.CurrentCulture);

    public string PriorityText => localizer.GetText($"Priority{Priority}");

    public string DetailText => $"{DueText} • {PriorityText}";

    public ReminderEntry ToEntry() => entry;

    public void Update(ReminderEntry value)
    {
        entry = value;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(DueAt));
        OnPropertyChanged(nameof(Priority));
        OnPropertyChanged(nameof(DueText));
        OnPropertyChanged(nameof(PriorityText));
        OnPropertyChanged(nameof(DetailText));
    }

    [RelayCommand]
    private Task EditAsync() => edit(this);

    [RelayCommand]
    private Task RemoveAsync() => remove(this);
}
