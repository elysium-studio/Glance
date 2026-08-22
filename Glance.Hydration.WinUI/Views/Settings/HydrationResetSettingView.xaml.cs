using Microsoft.UI.Xaml.Controls;

namespace Glance.Hydration.WinUI;

public sealed partial class HydrationResetSettingView :
    UserControl
{
    public HydrationResetSettingView() => InitializeComponent();

    public HydrationResetSettingViewModel ViewModel => (HydrationResetSettingViewModel)DataContext;
}
