using Glance.Shell;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class UninstallModuleDialog :
    ContentDialog
{
    public UninstallModuleDialog(ModuleSettingsItemViewModel viewModel)
    {
        InitializeComponent();
        Title = viewModel.UninstallDialogTitle;
        MessageText.Text = viewModel.UninstallDialogMessage;
        DataMessageText.Text = viewModel.UninstallDialogDataMessage;
        PrimaryButtonText = viewModel.UninstallDialogPrimaryButtonText;
        CloseButtonText = viewModel.UninstallDialogCloseButtonText;
    }
}
