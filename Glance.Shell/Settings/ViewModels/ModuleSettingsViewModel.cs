using Glance.Application.Abstractions;
using System.Collections.ObjectModel;

namespace Glance.Shell;

public sealed class ModuleSettingsViewModel :
    ObservableCollection<IGlanceModuleSettingViewModel>,
    ISettingViewModel
{
    private readonly IReadOnlyList<IGlanceModuleSettingViewModel> availableSettings;
    private bool disposed;

    public ModuleSettingsViewModel(IEnumerable<IGlanceModuleSettingViewModel> settings)
    {
        availableSettings = [.. settings];
    }

    public bool HasSettings => availableSettings.Count > 0;

    public void SetEnabled(bool enabled)
    {
        if (disposed)
        {
            return;
        }

        Clear();

        if (!enabled)
        {
            return;
        }

        foreach (IGlanceModuleSettingViewModel setting in availableSettings)
        {
            Add(setting);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Clear();

        foreach (IGlanceModuleSettingViewModel setting in availableSettings)
        {
            setting.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
