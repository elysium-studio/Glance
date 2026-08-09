using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class AboutDialog :
    UserControl
{
    public AboutDialog(AboutViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public AboutViewModel ViewModel { get; }
}
