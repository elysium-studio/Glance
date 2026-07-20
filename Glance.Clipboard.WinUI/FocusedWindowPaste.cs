using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Glance.Clipboard.WinUI;

internal static class FocusedWindowPaste
{
    public static bool Send()
    {
        INPUT[] inputs =
        [
            CreateKey(VIRTUAL_KEY.VK_CONTROL),
            CreateKey(VIRTUAL_KEY.VK_V),
            CreateKey(VIRTUAL_KEY.VK_V, KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP),
            CreateKey(VIRTUAL_KEY.VK_CONTROL, KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP)
        ];

        return PInvoke.SendInput(inputs, Marshal.SizeOf<INPUT>()) == inputs.Length;
    }

    private static INPUT CreateKey(
        VIRTUAL_KEY key,
        KEYBD_EVENT_FLAGS flags = 0)
    {
        INPUT input = new() { type = INPUT_TYPE.INPUT_KEYBOARD };
        input.Anonymous.ki.wVk = key;
        input.Anonymous.ki.dwFlags = flags;
        return input;
    }
}
