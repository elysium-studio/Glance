using Elysium.Application.Abstractions;

namespace Glance.Tests;

internal sealed class ImmediateTestDispatcher :
    IDispatcher
{
    public void Dispatch(Action action) => action();
}
