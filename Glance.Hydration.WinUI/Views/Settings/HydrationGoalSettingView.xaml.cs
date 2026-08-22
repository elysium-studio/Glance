using Microsoft.UI.Xaml.Controls;

namespace Glance.Hydration.WinUI;

public sealed partial class HydrationGoalSettingView :
    UserControl
{
    public HydrationGoalSettingView() => InitializeComponent();

    public HydrationGoalSettingViewModel ViewModel => (HydrationGoalSettingViewModel)DataContext;
}
