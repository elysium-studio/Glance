namespace Glance.Application.Abstractions;

public interface IGlanceComponent
{
    string Id { get; }

    string DisplayName { get; }

    string Description { get; }

    string SettingsCategory => GlanceModuleCategories.Other;

    string AccentResourceKey => Id switch
    {
        "AudioSwitcher" => "GlanceAudioSwitcherIconBrush",
        "Clipboard" => "GlanceClipboardIconBrush",
        "ColorPicker" => "GlanceColorPickerIconBrush",
        "DevicePresence" => "GlanceDevicePresenceIconBrush",
        "DropShelf" => "GlanceDropShelfIconBrush",
        "FocusSession" => "GlanceFocusSessionIconBrush",
        "Infinity" => "GlanceInfinityIconBrush",
        "KeepAwake" => "GlanceKeepAwakeIconBrush",
        "Magnifier" => "GlanceMagnifierIconBrush",
        "Media" => "GlanceMediaIconBrush",
        "Power" => "GlancePowerIconBrush",
        "Presence" => "GlancePresenceIconBrush",
        "PrivacyControls" => "GlancePrivacyControlsIconBrush",
        "QuickConvert" => "GlanceQuickConvertIconBrush",
        "Reminders" => "GlanceRemindersIconBrush",
        "RemovableDevices" => "GlanceRemovableDevicesIconBrush",
        "ScreenCapture" => "GlanceScreenCaptureIconBrush",
        "ScreenLens" => "GlanceScreenLensIconBrush",
        "ScreenRecorder" => "GlanceScreenRecorderIconBrush",
        "Stash" => "GlanceStashIconBrush",
        "Stopwatch" => "GlanceStopwatchIconBrush",
        "SystemMonitor" => "GlanceSystemIconBrush",
        "ThemeSwitcher" => "GlanceThemeSwitcherIconBrush",
        "Timer" => "GlanceTimerIconBrush",
        "VoiceNotes" => "GlanceVoiceNotesIconBrush",
        "Weather" => "GlanceWeatherAccentBrush",
        "WorldClock" => "GlanceWorldClockIconBrush",
        _ => "AccentTextFillColorPrimaryBrush"
    };

    string IconGlyph => Id switch
    {
        "AudioSwitcher" => "\uE767",
        "Clipboard" => "\uF0E3",
        "ColorPicker" => "\uE790",
        "DevicePresence" => "\uE702",
        "DropShelf" => "\uE8B7",
        "FocusSession" => "\uE708",
        "Infinity" => "\uE8A5",
        "KeepAwake" => "\uE7E8",
        "Magnifier" => "\uE8A3",
        "Media" => "\uE8D6",
        "Power" => "\uE945",
        "Presence" => "\uE77B",
        "PrivacyControls" => "\uE720",
        "QuickConvert" => "\uE8B1",
        "Reminders" => "\uE823",
        "RemovableDevices" => "\uE88E",
        "ScreenCapture" => "\uE722",
        "ScreenLens" => "\uE8C8",
        "ScreenRecorder" => "\uE714",
        "Stash" => "\uE718",
        "Stopwatch" => "\uE916",
        "SystemMonitor" => "\uEEA1",
        "ThemeSwitcher" => "\uE706",
        "Timer" => "\uE917",
        "VoiceNotes" => "\uE720",
        "Weather" => "\uE706",
        "WorldClock" => "\uE121",
        _ => string.Empty
    };

    string IconFontFamily => "Segoe Fluent Icons";

    int Order { get; }

    object CompactContent { get; }

    object ExpandedContent { get; }
}
