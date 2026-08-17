using Elysium.Platform.Abstractions;
using Glance.Application.Abstractions;
using System;
using System.Runtime.InteropServices;

namespace Glance.Shell.WinUI;

internal sealed class DesktopIslandScreenTargetProvider :
    IDesktopIslandScreenTargetProvider
{
    public GlanceScreenRectangle? GetTarget(WindowHandle window)
    {
        if (!GetWindowRect(window.Value, out DesktopIslandNativeRect windowBounds))
        {
            return null;
        }

        int width = Math.Max(1, windowBounds.Right - windowBounds.Left);
        int height = Math.Max(1, windowBounds.Bottom - windowBounds.Top);
        const int targetWidth = 64;
        const int targetHeight = 40;
        return new GlanceScreenRectangle(windowBounds.Left + ((width - targetWidth) / 2), windowBounds.Top + ((height - targetHeight) / 2), targetWidth, targetHeight);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out DesktopIslandNativeRect bounds);
}
