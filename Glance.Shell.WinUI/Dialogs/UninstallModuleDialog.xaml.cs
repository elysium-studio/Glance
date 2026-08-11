using Glance.Application.Abstractions;
using Microsoft.UI.Xaml.Controls;
using System.Globalization;

namespace Glance.Shell.WinUI;

public sealed partial class UninstallModuleDialog :
    ContentDialog
{
    public UninstallModuleDialog(string moduleName,
        ITextLocalizer localizer)
    {
        InitializeComponent();
        Title = string.Format(CultureInfo.CurrentCulture, localizer.GetText("UninstallModuleDialogTitle"), moduleName);
        MessageText.Text = string.Format(CultureInfo.CurrentCulture, localizer.GetText("UninstallModuleDialogMessage"), moduleName);
        DataMessageText.Text = localizer.GetText("UninstallModuleDialogDataMessage");
        PrimaryButtonText = localizer.GetText("UninstallModuleDialogPrimaryButton");
        CloseButtonText = localizer.GetText("UninstallModuleDialogCloseButton");
    }
}
