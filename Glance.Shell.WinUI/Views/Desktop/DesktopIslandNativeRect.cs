using System.Runtime.InteropServices;

namespace Glance.Shell.WinUI;

[StructLayout(LayoutKind.Sequential)]
internal struct DesktopIslandNativeRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}
