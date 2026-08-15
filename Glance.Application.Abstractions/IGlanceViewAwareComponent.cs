namespace Glance.Application.Abstractions;

public interface IGlanceViewAwareComponent
{
    void EnterView();

    void LeaveView();
}
