namespace Glance.Shell;

public sealed class ModuleSettingsNavigationRequestedEventArgs
{
    public ModuleSettingsNavigationRequestedEventArgs(ModuleSettingsItemViewModel module)
    {
        ArgumentNullException.ThrowIfNull(module);
        Module = module;
    }

    public ModuleSettingsItemViewModel Module { get; }
}

public sealed class ModuleReorderingRequestedEventArgs
{
}
