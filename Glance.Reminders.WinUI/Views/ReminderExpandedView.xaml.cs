using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Reminders.WinUI;

public sealed partial class ReminderExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<ReminderModule> localizer;

    public ReminderExpandedView(ReminderViewModel viewModel,
        ModuleResourceTextLocalizer<ReminderModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public ReminderViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string AddLabel => localizer.GetText("AddReminder");

    public string EditLabel => localizer.GetText("EditReminder");

    public string DeleteLabel => localizer.GetText("DeleteReminder");

    public string EmptySummary => localizer.GetText("EmptySummary");

    public string EmptyDetail => localizer.GetText("EmptyDetail");

    private Visibility WhenEmpty(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    private Visibility WhenPopulated(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    private string ToUpper(string value) => value.ToUpperInvariant();
}
