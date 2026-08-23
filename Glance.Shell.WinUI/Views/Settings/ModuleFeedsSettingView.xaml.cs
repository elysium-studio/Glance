using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Glance.Shell.WinUI;

public sealed partial class ModuleFeedsSettingView :
    UserControl
{
    public ModuleFeedsSettingView() => InitializeComponent();

    public ModuleFeedsSettingViewModel ViewModel => (ModuleFeedsSettingViewModel)DataContext;

    private async void HandleAddClick(object sender, RoutedEventArgs args)
    {
        if (XamlRoot is null)
        {
            return;
        }

        await ViewModel.ShowAddDialogAsync(XamlRoot);
    }

    private void HandleRemoveClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { CommandParameter: ModuleFeedSettingItemViewModel item })
        {
            _ = ViewModel.RemoveAsync(item);
        }
    }
}
