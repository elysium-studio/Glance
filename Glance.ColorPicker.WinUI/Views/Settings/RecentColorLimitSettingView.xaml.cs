using Microsoft.UI.Xaml.Controls;

namespace Glance.ColorPicker.WinUI;

public sealed partial class RecentColorLimitSettingView : UserControl
{
    public RecentColorLimitSettingView() => InitializeComponent();

    public RecentColorLimitSettingViewModel ViewModel => (RecentColorLimitSettingViewModel)DataContext;
}
