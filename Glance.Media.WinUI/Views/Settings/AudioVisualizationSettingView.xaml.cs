using Microsoft.UI.Xaml.Controls;

namespace Glance.Media.WinUI;

public sealed partial class AudioVisualizationSettingView : UserControl
{
    public AudioVisualizationSettingView() => InitializeComponent();

    public AudioVisualizationSettingViewModel ViewModel => (AudioVisualizationSettingViewModel)DataContext;
}
