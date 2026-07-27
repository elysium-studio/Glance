using Microsoft.UI.Xaml.Controls;

namespace Glance.Stopwatch.WinUI;

public sealed partial class ResumeAutomaticallySettingView :
    UserControl
{
    public ResumeAutomaticallySettingView() => InitializeComponent();

    public ResumeAutomaticallySettingViewModel ViewModel => (ResumeAutomaticallySettingViewModel)DataContext;
}
