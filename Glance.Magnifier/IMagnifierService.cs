namespace Glance.Magnifier;

public interface IMagnifierService :
    IDisposable
{
    MagnifierState GetState();

    bool Start();

    bool ZoomIn();

    bool ZoomOut();

    bool Close();
}
