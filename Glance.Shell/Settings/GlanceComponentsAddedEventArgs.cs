using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed class GlanceComponentsAddedEventArgs(
    IReadOnlyList<IGlanceComponent> components,
    Func<IReadOnlyList<IGlanceModuleSettingViewModel>> createSettings) :
    EventArgs
{
    public IReadOnlyList<IGlanceComponent> Components { get; } = components;

    public Func<IReadOnlyList<IGlanceModuleSettingViewModel>> CreateSettings { get; } = createSettings;
}
