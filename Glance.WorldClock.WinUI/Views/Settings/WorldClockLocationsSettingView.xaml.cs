using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.WorldClock.WinUI;

public sealed partial class WorldClockLocationsSettingView :
    UserControl
{
    public WorldClockLocationsSettingView() => InitializeComponent();

    public WorldClockLocationsSettingViewModel ViewModel => (WorldClockLocationsSettingViewModel)DataContext;

    private async void HandleAddClockClicked(object sender,
        RoutedEventArgs args) => await ViewModel.AddClockAsync();

    private async void HandleRemoveClockClicked(object sender,
        RoutedEventArgs args)
    {
        if (sender is FrameworkElement { Tag: WorldClockTimeZoneOption clock })
        {
            await ViewModel.RemoveClockAsync(clock);
        }
    }

    private async void HandleClocksReordered(ListViewBase sender,
        DragItemsCompletedEventArgs args) => await ViewModel.SaveOrderAsync();
}
