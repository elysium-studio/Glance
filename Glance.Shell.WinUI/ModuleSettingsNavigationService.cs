using System;

namespace Glance.Shell.WinUI;

public sealed class ModuleSettingsNavigationService
{
    public ModuleSettingsItemViewModel? CurrentModule { get; private set; }

    public event EventHandler? CurrentModuleChanged;

    public void NavigateTo(ModuleSettingsItemViewModel module)
    {
        ArgumentNullException.ThrowIfNull(module);

        if (!module.CanOpenSettings ||
            ReferenceEquals(CurrentModule, module))
        {
            return;
        }

        CurrentModule = module;
        CurrentModuleChanged?.Invoke(this, EventArgs.Empty);
    }

    public void GoBack()
    {
        if (CurrentModule is null)
        {
            return;
        }

        CurrentModule = null;
        CurrentModuleChanged?.Invoke(this, EventArgs.Empty);
    }
}
