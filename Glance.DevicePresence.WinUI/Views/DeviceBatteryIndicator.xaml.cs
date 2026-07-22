using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.DevicePresence.WinUI;

public sealed partial class DeviceBatteryIndicator :
    UserControl
{
    public static readonly DependencyProperty BatteryLevelProperty =
        DependencyProperty.Register(nameof(BatteryLevel),
            typeof(int), typeof(DeviceBatteryIndicator),
            new PropertyMetadata(-1, HandleBatteryLevelChanged));

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
        BatteryGlyph.Glyph = GetBatteryGlyph(BatteryLevel);
    }

    private static string GetBatteryGlyph(int batteryLevel) => batteryLevel switch
    {
        >= 100 => "\uE83F",
        >= 90 => "\uE859",
        >= 80 => "\uE858",
        >= 70 => "\uE857",
        >= 60 => "\uE856",
        >= 50 => "\uE855",
        >= 40 => "\uE854",
        >= 30 => "\uE853",
        >= 20 => "\uE852",
        >= 10 => "\uE851",
        _ => "\uE850"
    };
}
