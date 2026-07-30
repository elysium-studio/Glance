using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Glance.ScreenLens.WinUI;

internal sealed class LensTextSelectionSurface :
    Canvas
{
    public LensTextSelectionSurface()
    {
        Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.IBeam);
    }
}
