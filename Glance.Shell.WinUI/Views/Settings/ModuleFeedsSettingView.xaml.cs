using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class ModuleFeedsSettingView :
    UserControl
{
    public ModuleFeedsSettingView() => InitializeComponent();

    public ModuleFeedsSettingViewModel ViewModel => (ModuleFeedsSettingViewModel)DataContext;

    private void HandleAddClick(object sender, RoutedEventArgs args) => _ = ViewModel.AddAsync();

    private void HandleRemoveClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { CommandParameter: ModuleFeedSettingItemViewModel item })
        {
            _ = ViewModel.RemoveAsync(item);
        }
    }
}
