using Microsoft.UI.Xaml.Controls;

namespace Glance.WorldClock.WinUI;

public sealed partial class WorldClockTimeFormatSettingView :
    UserControl
{
    public WorldClockTimeFormatSettingView() => InitializeComponent();

    public WorldClockTimeFormatSettingViewModel ViewModel => (WorldClockTimeFormatSettingViewModel)DataContext;
}
