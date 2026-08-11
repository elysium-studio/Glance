using Glance.Application.Abstractions;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class RestartForModuleUpdateDialog :
    ContentDialog
{
    public RestartForModuleUpdateDialog(ITextLocalizer localizer)
    {
        InitializeComponent();
        Title = localizer.GetText("ModuleUpdateRestartDialogTitle");
        MessageText.Text = localizer.GetText("ModuleUpdateRestartDialogMessage");
        PrimaryButtonText = localizer.GetText("ModuleUpdateRestartDialogPrimaryButton");
        CloseButtonText = localizer.GetText("ModuleUpdateRestartDialogCloseButton");
    }
}
