using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace Glance.ThemeSwitcher.WinUI;

public sealed partial class WindowsSystemThemeService
{
    private const nint BroadcastWindow = 0xFFFF;
    private const uint SettingChangeMessage = 0x001A;
    private const string ThemeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public ThemeVariant CurrentTheme
    {
        get
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(ThemeKeyPath);
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0
                ? ThemeVariant.Dark
                : ThemeVariant.Light;
        }
    }

    public void Apply(ThemeVariant theme)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(ThemeKeyPath, true);
        int value = theme == ThemeVariant.Light ? 1 : 0;
        key.SetValue("AppsUseLightTheme", value, RegistryValueKind.DWord);
        key.SetValue("SystemUsesLightTheme", value, RegistryValueKind.DWord);
        _ = SendNotifyMessage(BroadcastWindow, SettingChangeMessage, 0, "ImmersiveColorSet");
    }

    [LibraryImport("user32.dll", EntryPoint = "SendNotifyMessageW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SendNotifyMessage(nint window,
        uint message,
        nuint parameter,
        string setting);
}
