using Microsoft.UI.Xaml.Input;

namespace Glance.Shell.WinUI;

public interface IDesktopIslandComponentController
{
    bool IsPointerOverIsland { get; }

    void Attach(IDesktopIslandComponentHost host);

    void Detach();

    void SelectedComponentChanged();

    void VisibilityChanged();

    void ThemeChanged();

    void PointerEntered();

    void PointerExited();

    bool RetainInteractionWithinRegion();

    void StopInteractionExit();

    void ButtonPressed(PointerRoutedEventArgs args);

    void ButtonReleased();

    void ApplyActivationMode();

    void ApplyExpansionLock();

    void IslandDeactivated();
}
