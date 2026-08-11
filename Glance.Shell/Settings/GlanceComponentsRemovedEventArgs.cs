using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed class GlanceComponentsRemovedEventArgs(IReadOnlyList<IGlanceComponent> components) :
    EventArgs
{
    public IReadOnlyList<IGlanceComponent> Components { get; } = components;
}
