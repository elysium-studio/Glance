using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.WorldClock.WinUI;

public sealed partial class WorldClockLocationsSettingView :
    UserControl
{
    public WorldClockLocationsSettingView() => InitializeComponent();

    public WorldClockLocationsSettingViewModel ViewModel => (WorldClockLocationsSettingViewModel)DataContext;

    private async void HandleAddClockClicked(object sender,
        RoutedEventArgs args) => await ViewModel.ShowAddClockDialogAsync(XamlRoot);

    private async void HandleRemoveClockClicked(object sender,
        RoutedEventArgs args)
    {
        if (sender is FrameworkElement { Tag: WorldClockTimeZoneOption clock })
        {
            await ViewModel.RemoveClockAsync(clock);
        }
    }

    private async void HandleMoveClockUpClicked(object sender,
        RoutedEventArgs args)
    {
        if (sender is FrameworkElement { Tag: WorldClockTimeZoneOption clock })
        {
            await ViewModel.MoveClockAsync(clock, -1);
        }
    }

    private async void HandleMoveClockDownClicked(object sender,
        RoutedEventArgs args)
    {
        if (sender is FrameworkElement { Tag: WorldClockTimeZoneOption clock })
        {
            await ViewModel.MoveClockAsync(clock, 1);
        }
    }

    private Visibility WhenEmpty(bool hasClocks) => hasClocks ? Visibility.Collapsed : Visibility.Visible;

    private Visibility WhenPopulated(bool hasClocks) => hasClocks ? Visibility.Visible : Visibility.Collapsed;
}
