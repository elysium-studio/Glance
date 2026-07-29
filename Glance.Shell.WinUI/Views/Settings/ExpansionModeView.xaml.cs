using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class ExpansionModeView :
    UserControl
{
    public ExpansionModeView() => InitializeComponent();

    public ExpansionModeViewModel ViewModel => (ExpansionModeViewModel)DataContext;

    public int SelectedExpansionModeIndex
    {
        get => ViewModel.Value switch
        {
            (int)GlanceExpansionMode.AlwaysExpanded => 1,
            _ => 0
        };
        set => ViewModel.Value = value switch
        {
            1 => (int)GlanceExpansionMode.AlwaysExpanded,
            _ => (int)GlanceExpansionMode.ExpandOnHover
        };
    }
}
