namespace Glance.Application.Abstractions;

public interface IGlanceIslandActivationComponent
{
    bool RequiresIslandActivation { get; }

    event EventHandler? IslandActivationRequirementChanged;
}
