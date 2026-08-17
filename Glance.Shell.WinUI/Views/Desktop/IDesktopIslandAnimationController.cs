namespace Glance.Shell.WinUI;

public interface IDesktopIslandAnimationController
{
    void Attach(IDesktopIslandAnimationHost host);

    void Detach();

    void CancelConnectedAnimation();

    void ExpandedChanged();

    void SelectedIndexChanged();
}
