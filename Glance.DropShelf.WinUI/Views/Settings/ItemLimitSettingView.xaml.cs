using Microsoft.UI.Xaml.Controls;

namespace Glance.DropShelf.WinUI;

public sealed partial class ItemLimitSettingView : UserControl
{
    public ItemLimitSettingView() => InitializeComponent();

    public ItemLimitSettingViewModel ViewModel => (ItemLimitSettingViewModel)DataContext;
}
