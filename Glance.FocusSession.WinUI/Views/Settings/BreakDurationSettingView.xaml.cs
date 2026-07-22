using Microsoft.UI.Xaml.Controls;

namespace Glance.FocusSession.WinUI;

public sealed partial class BreakDurationSettingView : UserControl
{
    public BreakDurationSettingView() => InitializeComponent();

    public BreakDurationSettingViewModel ViewModel => (BreakDurationSettingViewModel)DataContext;
}
