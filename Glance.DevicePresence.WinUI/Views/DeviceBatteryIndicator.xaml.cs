using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.DevicePresence.WinUI;

public sealed partial class DeviceBatteryIndicator :
    UserControl
{
    public static readonly DependencyProperty BatteryLevelProperty = DependencyProperty.Register(nameof(BatteryLevel), typeof(int), typeof(DeviceBatteryIndicator), new PropertyMetadata(-1, HandleBatteryLevelChanged));

    public DeviceBatteryIndicator()
    {
        InitializeComponent();
        UpdateBatteryLevel();
    }

    public int BatteryLevel
    {
        get => (int)GetValue(BatteryLevelProperty);
        set => SetValue(BatteryLevelProperty, value);
    }

    private static void HandleBatteryLevelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((DeviceBatteryIndicator)sender).UpdateBatteryLevel();

    private void UpdateBatteryLevel()
    {
        bool hasBatteryLevel = BatteryLevel is >= 0 and <= 100;
        Visibility = hasBatteryLevel ? Visibility.Visible : Visibility.Collapsed;

        if (!hasBatteryLevel)
        {
            return;
        }

        PercentageText.Text = $"{BatteryLevel}%";
        BatteryFill.Width = BatteryLevel == 0 ? 0 : Math.Max(1, 11 * BatteryLevel / 100d);
    }
}
