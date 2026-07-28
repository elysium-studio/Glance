namespace Glance.Magnifier;

public interface IMagnifierService :
    IDisposable
{
    MagnifierState GetState();

    bool ZoomIn();

    bool ZoomOut();

    bool Close();
}
