using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed partial class ModuleAttentionSettingViewModel(string moduleId,
    bool isEnabled,
    Func<bool, Task> setEnabled) :
    ObservableObject,
    IGlanceModuleSettingViewModel
{
    public string ModuleId { get; } = moduleId;

    public int Order => 0;

    [ObservableProperty]
    private bool value = isEnabled;

    partial void OnValueChanged(bool value) =>
        _ = setEnabled(value);

    public void Dispose() => GC.SuppressFinalize(this);
}
