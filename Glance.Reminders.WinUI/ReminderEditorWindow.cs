using Elysium.UI.Controls.WinUI;
using Glance.UI.WinUI;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Glance.Reminders.WinUI;

internal sealed partial class ReminderEditorWindow
{
    private readonly DatePicker datePicker;
    private readonly ContentDialogWindow dialog;
    private readonly TextBlock errorText;
    private readonly ModuleResourceTextLocalizer<ReminderModule> localizer;
    private readonly WindowId ownerWindowId;
    private readonly ComboBox priorityPicker;
    private readonly TextBox titleBox;
    private readonly TimePicker timePicker;
    private ReminderDraft? result;

    private ReminderEditorWindow(ReminderDraft? draft, ModuleResourceTextLocalizer<ReminderModule> localizer, WindowId ownerWindowId)
    {
        this.localizer = localizer;
        this.ownerWindowId = ownerWindowId;

        DateTimeOffset initialDueAt = draft?.DueAt.ToLocalTime() ?? DateTimeOffset.Now.AddHours(1);
        titleBox = new TextBox { Header = localizer.GetText("ReminderText"), Text = draft?.Title ?? string.Empty, Width = 360 };
        datePicker = new DatePicker { Header = localizer.GetText("Date"), Date = initialDueAt, MinYear = DateTimeOffset.Now.Date, Width = 360 };
        timePicker = new TimePicker { Header = localizer.GetText("Time"), Time = initialDueAt.TimeOfDay, ClockIdentifier = "12HourClock", Width = 360 };
        priorityPicker = new ComboBox
        {
            Header = localizer.GetText("Priority"),
            Width = 360,
            ItemsSource = new[] { localizer.GetText("PriorityLow"), localizer.GetText("PriorityNormal"), localizer.GetText("PriorityHigh") },
            SelectedIndex = (int)(draft?.Priority ?? ReminderPriority.Normal)
        };
        errorText = new TextBlock
        {
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 196, 43, 28)),
            Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.Wrap
        };

        StackPanel content = new() { Spacing = 12 };
        content.Children.Add(titleBox);
        content.Children.Add(datePicker);
        content.Children.Add(timePicker);
        content.Children.Add(priorityPicker);
        content.Children.Add(errorText);

        dialog = new ContentDialogWindow
        {
            Width = 408,
            Height = 520,
            Title = localizer.GetText(draft is null ? "AddReminder" : "EditReminder"),
            Content = content,
            PrimaryButtonText = localizer.GetText(draft is null ? "Add" : "Save"),
            CloseButtonText = localizer.GetText("Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        dialog.PrimaryButtonClick += HandlePrimaryButtonClick;
    }

    public static Task<ReminderDraft?> ShowAsync(ReminderDraft? draft, ModuleResourceTextLocalizer<ReminderModule> localizer, WindowId ownerWindowId) => new ReminderEditorWindow(draft, localizer, ownerWindowId).ShowAsync();

    private async Task<ReminderDraft?> ShowAsync()
    {
        try
        {
            ContentDialogResult dialogResult = await dialog.ShowAsync(ownerWindowId);
            return dialogResult == ContentDialogResult.Primary ? result : null;
        }
        finally
        {
            dialog.PrimaryButtonClick -= HandlePrimaryButtonClick;
        }
    }

    private void HandlePrimaryButtonClick(object? sender, ContentDialogWindowButtonClickEventArgs args)
    {
        string title = titleBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            ShowError(localizer.GetText("ReminderRequired"));
            args.Cancel = true;
            return;
        }

        DateTimeOffset selectedDate = datePicker.Date;
        DateTime localDueAt = selectedDate.LocalDateTime.Date + timePicker.Time;
        DateTimeOffset dueAt = new(localDueAt, TimeZoneInfo.Local.GetUtcOffset(localDueAt));

        if (dueAt <= DateTimeOffset.Now)
        {
            ShowError(localizer.GetText("FutureTimeRequired"));
            args.Cancel = true;
            return;
        }

        result = new ReminderDraft(title, dueAt, (ReminderPriority)priorityPicker.SelectedIndex);
    }

    private void ShowError(string message)
    {
        errorText.Text = message;
        errorText.Visibility = Visibility.Visible;
    }
}
