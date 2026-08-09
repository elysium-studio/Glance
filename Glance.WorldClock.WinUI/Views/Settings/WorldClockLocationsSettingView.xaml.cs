using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace Glance.WorldClock.WinUI;

public sealed partial class WorldClockLocationsSettingView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<WorldClockModule> localizer = new();

    public WorldClockLocationsSettingView() => InitializeComponent();

    public WorldClockLocationsSettingViewModel ViewModel => (WorldClockLocationsSettingViewModel)DataContext;

    private async void HandleAddClockClicked(object sender,
        RoutedEventArgs args)
    {
        IReadOnlyList<WorldClockTimeZoneOption> availableClocks = ViewModel.GetAvailableClocks();
        ListView clockPicker = new()
        {
            DisplayMemberPath = nameof(WorldClockTimeZoneOption.DisplayName),
            Height = 360,
            ItemsSource = availableClocks,
            SelectionMode = ListViewSelectionMode.Single
        };

        ContentDialog dialog = new()
        {
            Title = localizer.GetText("AddClock"),
            Content = clockPicker,
            Width = 560,
            PrimaryButtonText = localizer.GetText("Add"),
            CloseButtonText = localizer.GetText("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
            XamlRoot = XamlRoot
        };

        clockPicker.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = clockPicker.SelectedItem is WorldClockTimeZoneOption;

        if (await dialog.ShowAsync() == ContentDialogResult.Primary &&
            clockPicker.SelectedItem is WorldClockTimeZoneOption clock)
        {
            ViewModel.SelectedTimeZone = clock;
            await ViewModel.AddClockAsync();
        }
    }

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
