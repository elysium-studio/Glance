using Glance.Application.Abstractions;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class QuitDialog :
    ContentDialog
{
    public QuitDialog(ITextLocalizer localizer)
    {
        InitializeComponent();
        Title = localizer.GetText("QuitDialogTitle");
        MessageText.Text = localizer.GetText("QuitDialogMessage");
        PrimaryButtonText = localizer.GetText("QuitDialogPrimaryButton");
        CloseButtonText = localizer.GetText("QuitDialogCloseButton");
    }
}
