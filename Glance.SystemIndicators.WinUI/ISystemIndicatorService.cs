using Glance.SystemIndicators;

namespace Glance.SystemIndicators.WinUI;

public interface ISystemIndicatorService :
    IDisposable
{
    bool IsEnabled { get; set; }

    event EventHandler<SystemIndicatorState>? StateChanged;
}
