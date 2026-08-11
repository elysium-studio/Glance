using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using System.Globalization;

namespace Glance.Shell.WinUI;

public sealed partial class UninstallModuleDialog :
    ContentDialog
{
    public UninstallModuleDialog(string moduleName)
    {
        InitializeComponent();
        ResourceLoader resourceLoader = new();
        Title = string.Format(CultureInfo.CurrentCulture,
            resourceLoader.GetString("UninstallModuleDialogTitle"),
            moduleName);
        MessageText.Text = string.Format(CultureInfo.CurrentCulture,
            resourceLoader.GetString("UninstallModuleDialogMessage"),
            moduleName);
        DataMessageText.Text = resourceLoader.GetString("UninstallModuleDialogDataMessage");
        PrimaryButtonText = resourceLoader.GetString("UninstallModuleDialogPrimaryButton");
        CloseButtonText = resourceLoader.GetString("UninstallModuleDialogCloseButton");
    }
}
