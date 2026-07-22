using Microsoft.UI.Xaml.Controls;

namespace Glance.FocusSession.WinUI;

public sealed partial class FocusDurationSettingView : UserControl
{
    public FocusDurationSettingView() => InitializeComponent();

    public FocusDurationSettingViewModel ViewModel => (FocusDurationSettingViewModel)DataContext;
}
