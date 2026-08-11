namespace Glance.Shell;

public sealed class ModuleUninstalledEventArgs(IReadOnlyList<string> displayNames) :
    EventArgs
{
    public IReadOnlyList<string> DisplayNames { get; } = displayNames;
}
