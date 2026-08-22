using Microsoft.UI.Xaml.Controls;

namespace Glance.Fasting.WinUI;

public sealed partial class FastingPlanSettingView :
    UserControl
{
    public FastingPlanSettingView() => InitializeComponent();

    public FastingPlanSettingViewModel ViewModel => (FastingPlanSettingViewModel)DataContext;
}
