using Microsoft.UI.Xaml.Controls;

namespace Glance.Hydration.WinUI;

public sealed partial class HydrationReminderSettingsView :
    UserControl
{
    public HydrationReminderSettingsView() => InitializeComponent();

    public HydrationReminderSettingsViewModel ViewModel => (HydrationReminderSettingsViewModel)DataContext;
}
