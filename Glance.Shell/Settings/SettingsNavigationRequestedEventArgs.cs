namespace Glance.Shell;

public sealed class SettingsNavigationRequestedEventArgs(ISettingViewModel parent,
    ISettingViewModel target) :
    EventArgs
{
    public ISettingViewModel Parent { get; } = parent;

    public ISettingViewModel Target { get; } = target;
}
