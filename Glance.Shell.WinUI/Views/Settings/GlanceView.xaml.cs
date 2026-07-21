using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class GlanceView :
    UserControl
{
    public GlanceView() => InitializeComponent();

    public GlanceViewModel ViewModel => (GlanceViewModel)DataContext;
}
