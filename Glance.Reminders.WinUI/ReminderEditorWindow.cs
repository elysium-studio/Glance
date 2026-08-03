using Elysium.Platform.Windows;
using Glance.UI.WinUI;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;
using WinRT;
using WinRT.Interop;
using WinUIEx;
using PlatformWindowExtensions = Elysium.Platform.Windows.WindowExtensions;

namespace Glance.Reminders.WinUI;

internal sealed partial class ReminderEditorWindow
{
    private readonly TaskCompletionSource<ReminderDraft?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ContentDialog dialog;
    private readonly DatePicker datePicker;
    private readonly DisplayArea displayArea;
    private readonly TextBlock errorText;
    private readonly ModuleResourceTextLocalizer<ReminderModule> localizer;
    private readonly ComboBox priorityPicker;
    private readonly Grid root;
    private readonly Border smokeLayer;
    private readonly TextBox titleBox;
    private readonly TimePicker timePicker;
    private readonly Window window;
    private bool isClosed;

    private ReminderEditorWindow(ReminderDraft? draft,
        ModuleResourceTextLocalizer<ReminderModule> localizer,
        WindowId ownerWindowId)
    {
        this.localizer = localizer;
        displayArea = DisplayArea.GetFromWindowId(ownerWindowId, DisplayAreaFallback.Primary);
        DateTimeOffset initialDueAt = draft?.DueAt.ToLocalTime() ?? DateTimeOffset.Now.AddHours(1);
        titleBox = new TextBox
        {
            Header = localizer.GetText("ReminderText"),
            Text = draft?.Title ?? string.Empty,
            Width = 360
        };
        datePicker = new DatePicker
        {
            Header = localizer.GetText("Date"),
            Date = initialDueAt,
            MinYear = DateTimeOffset.Now.Date,
            Width = 360
        };
        timePicker = new TimePicker
        {
            Header = localizer.GetText("Time"),
            Time = initialDueAt.TimeOfDay,
            ClockIdentifier = "12HourClock",
            Width = 360
        };
        priorityPicker = new ComboBox
        {
            Header = localizer.GetText("Priority"),
            Width = 360,
            ItemsSource = new[]
            {
                localizer.GetText("PriorityLow"),
                localizer.GetText("PriorityNormal"),
                localizer.GetText("PriorityHigh")
            },
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
        dialog = new ContentDialog
        {
            Title = localizer.GetText(draft is null ? "AddReminder" : "EditReminder"),
            Content = content,
            PrimaryButtonText = localizer.GetText(draft is null ? "Add" : "Save"),
            CloseButtonText = localizer.GetText("Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        dialog.PrimaryButtonClick += HandlePrimaryButtonClick;
        dialog.Closing += HandleDialogClosing;
        dialog.Resources["ContentDialogSmokeFill"] = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        smokeLayer = new Border
        {
            Background = ResolveSmokeBrush(),
            IsHitTestVisible = false,
            Opacity = 0
        };
        root = new Grid { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)) };
        root.Children.Add(smokeLayer);
        root.Children.Add(dialog);
        root.Loaded += HandleRootLoaded;
        window = new Window
        {
            Content = root,
            ExtendsContentIntoTitleBar = true,
            SystemBackdrop = new TransparentTintBackdrop()
        };
        window.SetTitleBar(null);
        window.Closed += HandleWindowClosed;
        window.AppWindow.IsShownInSwitchers = false;
    }

    public static Task<ReminderDraft?> ShowAsync(ReminderDraft? draft,
        ModuleResourceTextLocalizer<ReminderModule> localizer,
        WindowId ownerWindowId) =>
        new ReminderEditorWindow(draft, localizer, ownerWindowId).ShowAsync();

    private Task<ReminderDraft?> ShowAsync()
    {
        AppWindow appWindow = window.AppWindow;
        OverlappedPresenter presenter = appWindow.Presenter.As<OverlappedPresenter>();
        presenter.IsAlwaysOnTop = true;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsResizable = false;
        presenter.SetBorderAndTitleBar(false, false);
        nint windowHandle = WindowNative.GetWindowHandle(window);
        PlatformWindowExtensions.SetBorderless(windowHandle, true);
        PlatformWindowExtensions.SetCornerRadius(windowHandle, WindowCornerPreference.Sharp);
        PlatformWindowExtensions.SetTopMost(windowHandle, true);
        appWindow.MoveAndResize(displayArea.OuterBounds);
        appWindow.Show(activateWindow: true);
        return completion.Task;
    }

    private void HandlePrimaryButtonClick(ContentDialog sender,
        ContentDialogButtonClickEventArgs args)
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

        completion.TrySetResult(new ReminderDraft(title, dueAt, (ReminderPriority)priorityPicker.SelectedIndex));
    }

    private async void HandleRootLoaded(object sender,
        RoutedEventArgs args)
    {
        root.Loaded -= HandleRootLoaded;

        try
        {
            AnimateSmoke(1);
            dialog.XamlRoot = root.XamlRoot;
            await dialog.ShowAsync(ContentDialogPlacement.InPlace);
            completion.TrySetResult(null);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
        finally
        {
            Close();
        }
    }

    private void HandleDialogClosing(ContentDialog sender,
        ContentDialogClosingEventArgs args) =>
        AnimateSmoke(0);

    private void HandleWindowClosed(object sender,
        WindowEventArgs args)
    {
        isClosed = true;
        completion.TrySetResult(null);
    }

    private void ShowError(string message)
    {
        errorText.Text = message;
        errorText.Visibility = Visibility.Visible;
    }

    private void AnimateSmoke(double opacity)
    {
        DoubleAnimation animation = new()
        {
            To = opacity,
            Duration = TimeSpan.FromMilliseconds(83)
        };
        Storyboard.SetTarget(animation, smokeLayer);
        Storyboard.SetTargetProperty(animation, nameof(UIElement.Opacity));
        Storyboard storyboard = new();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private static Brush ResolveSmokeBrush()
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("SmokeFillColorDefaultBrush", out object value) && value is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Windows.UI.Color.FromArgb(77, 0, 0, 0));
    }

    private void Close()
    {
        if (isClosed)
        {
            return;
        }

        isClosed = true;

        dialog.PrimaryButtonClick -= HandlePrimaryButtonClick;
        dialog.Closing -= HandleDialogClosing;
        window.Closed -= HandleWindowClosed;
        window.Close();
    }
}
