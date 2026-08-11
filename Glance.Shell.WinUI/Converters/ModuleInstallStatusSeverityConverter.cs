using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public static class ModuleInstallStatusSeverityConverter
{
    public static InfoBarSeverity Convert(ModuleInstallStatusKind kind) => kind switch
    {
        ModuleInstallStatusKind.Success => InfoBarSeverity.Success,
        ModuleInstallStatusKind.Warning => InfoBarSeverity.Warning,
        ModuleInstallStatusKind.Error => InfoBarSeverity.Error,
        _ => InfoBarSeverity.Informational
    };
}
