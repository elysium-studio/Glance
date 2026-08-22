using Microsoft.UI.Xaml.Controls;

namespace Glance.Hydration.WinUI;

public sealed partial class HydrationServingSettingView :
    UserControl
{
    public HydrationServingSettingView() => InitializeComponent();

    public HydrationServingSettingViewModel ViewModel => (HydrationServingSettingViewModel)DataContext;
}
