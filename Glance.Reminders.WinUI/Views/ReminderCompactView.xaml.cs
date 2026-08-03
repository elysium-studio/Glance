using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Reminders.WinUI;

public sealed partial class ReminderCompactView :
    UserControl
{
    public ReminderCompactView(ReminderViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public ReminderViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
